using System.Security.Claims;
using BudgetApp.Data.Repositories;
using BudgetApp.Enums;
using BudgetApp.Extensions;
using BudgetApp.Models;
using BudgetApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApp.Controllers
{
    [Authorize]
    public class InviteController : Controller
    {
        private readonly ILogger<InviteController> _logger;
        private readonly IBudgetRepository<BudgetModel> _budgetRepo;
        private readonly IBudgetUserRepository<BudgetUserModel> _budgetUserRepo;
        private readonly IBudgetInviteRepository<BudgetInviteModel> _inviteRepo;
        private readonly IUserRepository<UserModel> _userRepo;
        private readonly IEmailService _emailService;

        public InviteController(
            ILogger<InviteController> logger,
            IBudgetRepository<BudgetModel> budgetRepo,
            IBudgetUserRepository<BudgetUserModel> budgetUserRepo,
            IBudgetInviteRepository<BudgetInviteModel> inviteRepo,
            IUserRepository<UserModel> userRepo,
            IEmailService emailService
        )
        {
            _logger = logger;
            _budgetRepo = budgetRepo;
            _budgetUserRepo = budgetUserRepo;
            _inviteRepo = inviteRepo;
            _userRepo = userRepo;
            _emailService = emailService;
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> Form(int budgetId)
        {
            int userId = GetCurrentUserId();
            var budgetUsers = (await _budgetUserRepo.GetByBudgetId(budgetId)).ToList();
            if (!budgetUsers.IsMainLeaderFor(userId))
                return Forbid();

            return PartialView(
                "_InviteLeaderModal",
                new SendInviteViewModel { Email = string.Empty, BudgetId = budgetId }
            );
        }

        [HttpGet]
        public async Task<IActionResult> Pending()
        {
            int userId = GetCurrentUserId();
            var invites = (await _inviteRepo.GetPendingByInvitedUserId(userId)).ToList();
            var result = invites.Select(i => new
            {
                i.Id,
                i.BudgetId,
                InvitedBy = i.InvitedByDisplayName,
                BudgetName = i.BudgetName,
            });
            return Json(new { count = invites.Count, invites = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(SendInviteViewModel vm)
        {
            int userId = GetCurrentUserId();

            var budget = await _budgetRepo.GetById(vm.BudgetId);
            if (budget == null)
                return NotFound();

            var budgetUsers = (
                await _budgetUserRepo.GetByBudgetId(vm.BudgetId, includeInactive: true)
            ).ToList();
            bool isMainLeader = budgetUsers.IsMainLeaderFor(userId);
            if (!isMainLeader)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Nur die Hauptleitperson kann Einladungen versenden."
                );
                return PartialView("_InviteLeaderModal", vm);
            }

            if (!ModelState.IsValid)
                return PartialView("_InviteLeaderModal", vm);

            var targetUser = await _userRepo.GetByEmail(vm.Email.Trim().ToLowerInvariant());
            if (targetUser == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Kein Benutzer mit dieser E-Mail-Adresse gefunden."
                );
                return PartialView("_InviteLeaderModal", vm);
            }

            if (targetUser.Id == userId)
            {
                ModelState.AddModelError(string.Empty, "Sie können sich nicht selbst einladen.");
                return PartialView("_InviteLeaderModal", vm);
            }

            var existingMembership = budgetUsers.FirstOrDefault(bu => bu.UserId == targetUser.Id);
            if (existingMembership != null)
            {
                var message = existingMembership.IsActive
                    ? $"{targetUser.DisplayName} ist bereits Leitperson dieses Lagers."
                    : $"{targetUser.DisplayName} wurde entfernt. Bitte über \"Reaktivieren\" auf der Leitpersonen-Seite wiederherstellen.";
                ModelState.AddModelError(string.Empty, message);
                return PartialView("_InviteLeaderModal", vm);
            }

            var existingInvites = await _inviteRepo.GetByBudgetId(vm.BudgetId);
            var existingInvite = existingInvites.FirstOrDefault(i =>
                i.InvitedUserId == targetUser.Id
            );
            if (existingInvite != null && existingInvite.Status == InviteStatus.Pending)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"{targetUser.DisplayName} hat bereits eine ausstehende Einladung."
                );
                return PartialView("_InviteLeaderModal", vm);
            }

            try
            {
                if (existingInvite != null)
                {
                    // A declined invite for this BudgetId+InvitedUserId already exists —
                    // UQ_BudgetInvite_BudgetId_InvitedUserId forbids a second row, so revive it.
                    await _inviteRepo.Reinvite(existingInvite.Id, userId);
                }
                else
                {
                    var invite = new BudgetInviteModel
                    {
                        BudgetId = vm.BudgetId,
                        InvitedByUserId = userId,
                        InvitedUserId = targetUser.Id,
                        Status = InviteStatus.Pending,
                    };
                    await _inviteRepo.Create(invite);
                }

                try
                {
                    await _emailService.SendBudgetInviteEmailAsync(
                        targetUser.Email,
                        targetUser.DisplayName,
                        budget.Name,
                        Url.Action("Index", "Home", new { openInvites = "1" }, Request.Scheme)!
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error sending invite email to user {UserId} for budget {BudgetId}",
                        targetUser.Id,
                        vm.BudgetId
                    );
                }

                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Eingeladen",
                        Message = $"Einladung an {targetUser.DisplayName} gesendet.",
                        Type = ToastType.Success,
                    }
                );
                return Json(
                    new
                    {
                        success = true,
                        redirectUrl = Url.Action("Leaders", "Budget", new { id = vm.BudgetId }),
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending invite to user {UserId} for budget {BudgetId}",
                    targetUser.Id,
                    vm.BudgetId
                );
                ModelState.AddModelError(string.Empty, "Ein Fehler ist aufgetreten.");
                return PartialView("_InviteLeaderModal", vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reinvite(int id)
        {
            int userId = GetCurrentUserId();
            var invite = await _inviteRepo.GetById(id);
            if (invite == null)
                return NotFound();

            var budgetUsers = (await _budgetUserRepo.GetByBudgetId(invite.BudgetId)).ToList();
            bool isMainLeader = budgetUsers.Any(bu => bu.UserId == userId && bu.IsMainLeader);
            if (!isMainLeader)
            {
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Nur die Hauptleitperson kann Einladungen versenden.",
                        Type = ToastType.Error,
                    }
                );
                return RedirectToAction("Leaders", "Budget", new { id = invite.BudgetId });
            }

            if (invite.Status != InviteStatus.Declined)
            {
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Diese Einladung kann nicht erneut gesendet werden.",
                        Type = ToastType.Warning,
                    }
                );
                return RedirectToAction("Leaders", "Budget", new { id = invite.BudgetId });
            }

            try
            {
                await _inviteRepo.Reinvite(id, userId);

                try
                {
                    await _emailService.SendBudgetInviteEmailAsync(
                        invite.InvitedUserEmail!,
                        invite.InvitedUserDisplayName!,
                        invite.BudgetName!,
                        Url.Action("Index", "Home", new { openInvites = "1" }, Request.Scheme)!
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending reinvite email for invite {InviteId}", id);
                }

                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Eingeladen",
                        Message = $"Einladung an {invite.InvitedUserDisplayName} erneut gesendet.",
                        Type = ToastType.Success,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reinviting for invite {InviteId}", id);
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Ein Fehler ist aufgetreten.",
                        Type = ToastType.Error,
                    }
                );
            }

            return RedirectToAction("Leaders", "Budget", new { id = invite.BudgetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            int userId = GetCurrentUserId();
            var invite = await _inviteRepo.GetById(id);

            if (invite == null)
                return NotFound();
            if (invite.InvitedUserId != userId)
                return Forbid();

            if (invite.Status != InviteStatus.Pending)
            {
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Diese Einladung ist nicht mehr ausstehend.",
                        Type = ToastType.Warning,
                    }
                );
                return RedirectToAction("Detail", "Budget", new { id = invite.BudgetId });
            }

            try
            {
                await _inviteRepo.UpdateStatus(id, InviteStatus.Accepted);

                var budgetUser = new BudgetUserModel
                {
                    BudgetId = invite.BudgetId,
                    UserId = userId,
                    IsMainLeader = false,
                };
                await _budgetUserRepo.Create(budgetUser);

                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Angenommen",
                        Message = "Einladung angenommen. Willkommen im Lager!",
                        Type = ToastType.Success,
                    }
                );
                return RedirectToAction("Detail", "Budget", new { id = invite.BudgetId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting invite {InviteId}", id);
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Ein Fehler ist aufgetreten.",
                        Type = ToastType.Error,
                    }
                );
                return RedirectToAction("Detail", "Budget", new { id = invite.BudgetId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decline(int id)
        {
            int userId = GetCurrentUserId();
            var invite = await _inviteRepo.GetById(id);

            if (invite == null)
                return NotFound();
            if (invite.InvitedUserId != userId)
                return Forbid();

            if (invite.Status != InviteStatus.Pending)
            {
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Diese Einladung ist nicht mehr ausstehend.",
                        Type = ToastType.Warning,
                    }
                );
                return RedirectToAction("Index", "Home");
            }

            try
            {
                await _inviteRepo.UpdateStatus(id, InviteStatus.Declined);

                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Abgelehnt",
                        Message = "Einladung abgelehnt.",
                        Type = ToastType.Info,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining invite {InviteId}", id);
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Ein Fehler ist aufgetreten.",
                        Type = ToastType.Error,
                    }
                );
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
