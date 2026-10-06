using System.Security.Claims;
using BudgetApp.Authorization;
using BudgetApp.Data.Repositories;
using BudgetApp.Enums;
using BudgetApp.Extensions;
using BudgetApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApp.Controllers
{
    [Authorize]
    public class TransactionController : Controller
    {
        private readonly ILogger<TransactionController> _logger;
        private readonly IBudgetRepository<BudgetModel> _budgetRepo;
        private readonly IBudgetUserRepository<BudgetUserModel> _budgetUserRepo;
        private readonly ITransactionRepository<TransactionModel> _transactionRepo;
        private readonly ITransactionDocumentRepository<TransactionDocumentModel> _docRepo;
        private readonly IPositionRepository<PositionModel> _positionRepo;

        private static readonly HashSet<string> AllowedContentTypes = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
            "application/pdf",
        };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public TransactionController(
            ILogger<TransactionController> logger,
            IBudgetRepository<BudgetModel> budgetRepo,
            IBudgetUserRepository<BudgetUserModel> budgetUserRepo,
            ITransactionRepository<TransactionModel> transactionRepo,
            ITransactionDocumentRepository<TransactionDocumentModel> docRepo,
            IPositionRepository<PositionModel> positionRepo
        )
        {
            _logger = logger;
            _budgetRepo = budgetRepo;
            _budgetUserRepo = budgetUserRepo;
            _transactionRepo = transactionRepo;
            _docRepo = docRepo;
            _positionRepo = positionRepo;
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<bool> HasBudgetAccessAsync(int budgetId, int userId)
        {
            var budget = await _budgetRepo.GetById(budgetId);
            if (budget == null)
                return false;
            if (budget.CreatedByUserId == userId)
                return true;
            var budgetUsers = await _budgetUserRepo.GetByBudgetId(budgetId);
            return budgetUsers.Any(bu => bu.UserId == userId);
        }

        private async Task PopulateFormListsAsync(CreateTransactionViewModel vm)
        {
            vm.BudgetUsers = (await _budgetUserRepo.GetByBudgetId(vm.BudgetId)).ToList();
            vm.Positions = (await _positionRepo.GetByBudgetId(vm.BudgetId)).ToList();
        }

        private async Task SaveAttachmentsAsync(int transactionId, List<IFormFile> attachments)
        {
            foreach (var file in attachments)
            {
                if (file.Length == 0)
                    continue;

                if (!AllowedContentTypes.Contains(file.ContentType))
                {
                    _logger.LogWarning(
                        "Skipping file {FileName}: unsupported content type {ContentType}",
                        file.FileName,
                        file.ContentType
                    );
                    continue;
                }

                if (file.Length > MaxFileSizeBytes)
                {
                    _logger.LogWarning(
                        "Skipping file {FileName}: exceeds max size ({Size} bytes)",
                        file.FileName,
                        file.Length
                    );
                    continue;
                }

                try
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);

                    var doc = new TransactionDocumentModel
                    {
                        TransactionId = transactionId,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = (int)file.Length,
                        FileData = ms.ToArray(),
                    };
                    await _docRepo.Create(doc);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error saving attachment {FileName} for transaction {TransactionId}",
                        file.FileName,
                        transactionId
                    );
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(int budgetId)
        {
            int userId = GetCurrentUserId();
            if (!await HasBudgetAccessAsync(budgetId, userId))
                return Forbid();

            var vm = new CreateTransactionViewModel
            {
                BudgetId = budgetId,
                PerformedByUserId = userId,
                Name = string.Empty,
            };
            await PopulateFormListsAsync(vm);
            return PartialView("_CreateModal", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateTransactionViewModel vm,
            List<IFormFile>? attachments
        )
        {
            if (!ModelState.IsValid)
            {
                await PopulateFormListsAsync(vm);
                return PartialView("_CreateModal", vm);
            }

            int userId = GetCurrentUserId();
            if (!await HasBudgetAccessAsync(vm.BudgetId, userId))
                return Forbid();

            try
            {
                var transaction = new TransactionModel
                {
                    BudgetId = vm.BudgetId,
                    PerformedByUserId = vm.PerformedByUserId,
                    PositionId = vm.PositionId,
                    Name = vm.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(vm.Description)
                        ? null
                        : vm.Description.Trim(),
                    Amount = vm.Amount,
                    PaymentSource = vm.PaymentSource,
                    PaymentMethod = vm.PaymentMethod,
                };

                int newId = await _transactionRepo.Create(transaction);

                if (attachments != null && attachments.Count > 0)
                    await SaveAttachmentsAsync(newId, attachments);

                var toast = new ToastMessageViewModel
                {
                    Title = "Gespeichert",
                    Message = "Ausgabe erfolgreich erfasst.",
                    Type = ToastType.Success,
                };
                TempData.Put("ToastMsg", toast);

                string redirectUrl =
                    !string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)
                        ? vm.ReturnUrl
                        : Url.Action("Detail", "Budget", new { id = vm.BudgetId })!;
                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error creating transaction for budget {BudgetId}",
                    vm.BudgetId
                );
                ModelState.AddModelError(string.Empty, "Ein unerwarteter Fehler ist aufgetreten.");
                await PopulateFormListsAsync(vm);
                return PartialView("_CreateModal", vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            int userId = GetCurrentUserId();
            var transaction = await _transactionRepo.GetById(id);
            if (transaction == null)
                return NotFound();

            if (!await HasBudgetAccessAsync(transaction.BudgetId, userId))
                return Forbid();

            var existingDocs = (await _docRepo.GetByTransactionId(id)).ToList();

            var vm = new EditTransactionViewModel
            {
                Id = id,
                BudgetId = transaction.BudgetId,
                PerformedByUserId = transaction.PerformedByUserId,
                PositionId = transaction.PositionId,
                Name = transaction.Name,
                Description = transaction.Description,
                Amount = transaction.Amount,
                PaymentSource = transaction.PaymentSource,
                PaymentMethod = transaction.PaymentMethod,
                ExistingDocuments = existingDocs,
            };
            await PopulateFormListsAsync(vm);
            return PartialView("_EditModal", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            EditTransactionViewModel vm,
            List<IFormFile>? newAttachments
        )
        {
            if (!ModelState.IsValid)
            {
                vm.ExistingDocuments = (await _docRepo.GetByTransactionId(vm.Id)).ToList();
                await PopulateFormListsAsync(vm);
                return PartialView("_EditModal", vm);
            }

            int userId = GetCurrentUserId();
            var existing = await _transactionRepo.GetById(vm.Id);
            if (existing == null)
                return NotFound();

            if (!await HasBudgetAccessAsync(existing.BudgetId, userId))
                return Forbid();

            try
            {
                existing.PerformedByUserId = vm.PerformedByUserId;
                existing.PositionId = vm.PositionId;
                existing.Name = vm.Name.Trim();
                existing.Description = string.IsNullOrWhiteSpace(vm.Description)
                    ? null
                    : vm.Description.Trim();
                existing.Amount = vm.Amount;
                existing.PaymentSource = vm.PaymentSource;
                existing.PaymentMethod = vm.PaymentMethod;

                await _transactionRepo.Update(existing);

                if (newAttachments != null && newAttachments.Count > 0)
                    await SaveAttachmentsAsync(vm.Id, newAttachments);

                var toast = new ToastMessageViewModel
                {
                    Title = "Gespeichert",
                    Message = "Ausgabe erfolgreich aktualisiert.",
                    Type = ToastType.Success,
                };
                TempData.Put("ToastMsg", toast);

                string redirectUrl =
                    !string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)
                        ? vm.ReturnUrl
                        : Url.Action(
                            "UserTransactions",
                            new
                            {
                                budgetId = existing.BudgetId,
                                userId = existing.PerformedByUserId,
                            }
                        )!;
                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction {TransactionId}", vm.Id);
                ModelState.AddModelError(string.Empty, "Ein unerwarteter Fehler ist aufgetreten.");
                vm.ExistingDocuments = (await _docRepo.GetByTransactionId(vm.Id)).ToList();
                await PopulateFormListsAsync(vm);
                return PartialView("_EditModal", vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int budgetId)
        {
            int userId = GetCurrentUserId();
            var transaction = await _transactionRepo.GetById(id);
            if (transaction == null)
                return NotFound();

            if (!await HasBudgetAccessAsync(transaction.BudgetId, userId))
                return Forbid();

            try
            {
                await _transactionRepo.Delete(id);

                var toast = new ToastMessageViewModel
                {
                    Title = "Gelöscht",
                    Message = "Ausgabe erfolgreich gelöscht.",
                    Type = ToastType.Success,
                };
                TempData.Put("ToastMsg", toast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction {TransactionId}", id);
                var toast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Ein Fehler ist aufgetreten.",
                    Type = ToastType.Error,
                };
                TempData.Put("ToastMsg", toast);
            }

            return RedirectToAction("Detail", "Budget", new { id = budgetId });
        }

        [HttpGet]
        public async Task<IActionResult> UserTransactions(int budgetId, int userId)
        {
            int currentUserId = GetCurrentUserId();
            if (!await HasBudgetAccessAsync(budgetId, currentUserId))
                return Forbid();

            var budget = await _budgetRepo.GetById(budgetId);
            if (budget == null)
                return NotFound();

            var allSummaries = await _transactionRepo.GetUserSummariesByBudgetId(budgetId);
            var userSummary = allSummaries.FirstOrDefault(s => s.UserId == userId);
            if (userSummary == null)
            {
                userSummary = new UserExpenseSummaryViewModel { UserId = userId };
            }

            var transactions = (await _transactionRepo.GetByUserId(userId, budgetId)).ToList();

            // Load positions for name lookup
            var positions = (await _positionRepo.GetByBudgetId(budgetId)).ToDictionary(p => p.Id);

            var transactionsWithDocs = new List<TransactionWithDocumentsViewModel>();
            foreach (var t in transactions)
            {
                var docs = (await _docRepo.GetByTransactionId(t.Id)).ToList();
                transactionsWithDocs.Add(
                    new TransactionWithDocumentsViewModel
                    {
                        Transaction = t,
                        Documents = docs,
                        PerformedByDisplayName = userSummary.FullName,
                        PositionName =
                            t.PositionId.HasValue
                            && positions.TryGetValue(t.PositionId.Value, out var pos)
                                ? pos.Name
                                : null,
                    }
                );
            }

            var vm = new UserTransactionsViewModel
            {
                Budget = budget,
                UserSummary = userSummary,
                Transactions = transactionsWithDocs,
            };
            return View(vm);
        }

        [HttpGet]
        [Authorize(Policy = Policies.MainLeaderOnly)]
        public async Task<IActionResult> BudgetOverview(int budgetId)
        {
            var budget = await _budgetRepo.GetById(budgetId);
            if (budget == null)
                return NotFound();

            var vm = new TransactionBudgetOverviewViewModel
            {
                Budget = budget,
                UserSummaries = (
                    await _transactionRepo.GetUserSummariesByBudgetId(budgetId)
                ).ToList(),
            };
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            int userId = GetCurrentUserId();
            var doc = await _docRepo.GetById(id);
            if (doc == null)
                return NotFound();

            var transaction = await _transactionRepo.GetById(doc.TransactionId);
            if (transaction == null)
                return NotFound();

            if (!await HasBudgetAccessAsync(transaction.BudgetId, userId))
                return Forbid();

            return File(doc.FileData!, doc.ContentType, doc.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id, int transactionId)
        {
            int userId = GetCurrentUserId();

            // Load metadata (no FileData needed)
            var docs = await _docRepo.GetByTransactionId(transactionId);
            var doc = docs.FirstOrDefault(d => d.Id == id);
            if (doc == null)
                return NotFound();

            var transaction = await _transactionRepo.GetById(transactionId);
            if (transaction == null)
                return NotFound();

            if (!await HasBudgetAccessAsync(transaction.BudgetId, userId))
                return Forbid();

            // Called only from within the edit modal, so it responds via JSON
            // rather than redirecting back to Edit (which is a modal-only partial).
            try
            {
                await _docRepo.Delete(id);
                return Json(
                    new
                    {
                        success = true,
                        title = "Gelöscht",
                        message = "Dokument erfolgreich gelöscht.",
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return Json(
                    new
                    {
                        success = false,
                        title = "Fehler",
                        message = "Ein Fehler ist aufgetreten.",
                    }
                );
            }
        }
    }
}
