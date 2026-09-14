using System.Security.Claims;
using BudgetApp.Data.Repositories;
using BudgetApp.Enums;
using BudgetApp.Extensions;
using BudgetApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApp.Controllers
{
    [Authorize]
    public class BudgetController : Controller
    {
        private readonly ILogger<BudgetController> _logger;
        private readonly IBudgetRepository<BudgetModel> _budgetRepo;
        private readonly ITemplateBudgetRepository<TemplateBudgetModel> _templateBudgetRepo;
        private readonly ITemplatePositionRepository<TemplatePositionModel> _templatePositionRepo;
        private readonly IPositionRepository<PositionModel> _positionRepo;
        private readonly IPositionTypeRepository<PositionTypeModel> _positionTypeRepo;
        private readonly ICategoryRepository<CategoryModel> _categoryRepo;
        private readonly ISubCategoryRepository<SubCategoryModel> _subCategoryRepo;
        private readonly IBudgetUserRepository<BudgetUserModel> _budgetUserRepo;
        private readonly IBudgetInviteRepository<BudgetInviteModel> _inviteRepo;

        public BudgetController(
            ILogger<BudgetController> logger,
            IBudgetRepository<BudgetModel> budgetRepo,
            ITemplateBudgetRepository<TemplateBudgetModel> templateBudgetRepo,
            ITemplatePositionRepository<TemplatePositionModel> templatePositionRepo,
            IPositionRepository<PositionModel> positionRepo,
            IPositionTypeRepository<PositionTypeModel> positionTypeRepo,
            ICategoryRepository<CategoryModel> categoryRepo,
            ISubCategoryRepository<SubCategoryModel> subCategoryRepo,
            IBudgetUserRepository<BudgetUserModel> budgetUserRepo,
            IBudgetInviteRepository<BudgetInviteModel> inviteRepo
        )
        {
            _logger = logger;
            _budgetRepo = budgetRepo;
            _templateBudgetRepo = templateBudgetRepo;
            _templatePositionRepo = templatePositionRepo;
            _positionRepo = positionRepo;
            _positionTypeRepo = positionTypeRepo;
            _categoryRepo = categoryRepo;
            _subCategoryRepo = subCategoryRepo;
            _budgetUserRepo = budgetUserRepo;
            _inviteRepo = inviteRepo;
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<bool> HasAccessAsync(BudgetModel budget, int userId)
        {
            if (budget.CreatedByUserId == userId)
                return true;
            var budgetUsers = await _budgetUserRepo.GetByBudgetId(budget.Id);
            return budgetUsers.Any(bu => bu.UserId == userId);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var budgets = (await _budgetRepo.GetAllForUser(userId))
                .OrderByDescending(b => b.StartDate)
                .ToList();
            var leaderBudgetIds = (await _budgetUserRepo.GetByUserId(userId))
                .Where(bu => bu.IsMainLeader)
                .Select(bu => bu.BudgetId)
                .ToHashSet();

            var rows = budgets
                .Select(b => new BudgetIndexRowViewModel
                {
                    Budget = b,
                    IsMainLeader = leaderBudgetIds.Contains(b.Id),
                })
                .ToList();
            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            int userId = GetCurrentUserId();
            var vm = new BudgetFormViewModel
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                AvailableTemplates = (await _templateBudgetRepo.GetAllForUser(userId)).ToList(),
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BudgetFormViewModel vm)
        {
            int userId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                vm.AvailableTemplates = (await _templateBudgetRepo.GetAllForUser(userId)).ToList();
                return View(vm);
            }

            try
            {
                var budget = MapToModel(vm);
                budget.CreatedByUserId = userId;
                int newId = await _budgetRepo.Create(budget);

                // Auto-add creator as main leader
                await _budgetUserRepo.Create(
                    new BudgetUserModel
                    {
                        BudgetId = newId,
                        UserId = userId,
                        IsMainLeader = true,
                    }
                );

                if (vm.SelectedTemplateBudgetId > 0)
                {
                    var templatePositions = await _templatePositionRepo.GetByTemplateBudgetId(
                        vm.SelectedTemplateBudgetId.Value
                    );
                    foreach (var tp in templatePositions)
                    {
                        var position = new PositionModel
                        {
                            BudgetId = newId,
                            PositionTypeId = tp.PositionTypeId,
                            CategoryId = tp.CategoryId,
                            SubCategoryId = tp.SubCategoryId == 0 ? null : tp.SubCategoryId,
                            Name = tp.Name,
                            FixedAmount_fc = tp.FixedAmount ?? 0,
                            QuantityVar_fc = string.IsNullOrEmpty(tp.QuantityVar)
                                ? null
                                : tp.QuantityVar + "_fc",
                            Quantity_fc = string.IsNullOrEmpty(tp.QuantityVar)
                                ? (tp.Quantity ?? 0)
                                : 0m,
                            UnitAmount_fc = tp.UnitAmount ?? 0,
                            SortIndex = tp.SortIndex,
                        };
                        await _positionRepo.Create(position);
                    }
                }

                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Erfolg",
                        Message = "Budget erstellt.",
                        Type = ToastType.Success,
                    }
                );
                return RedirectToAction(nameof(Detail), new { id = newId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Create");
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Ein unerwarteter Fehler ist aufgetreten.",
                        Type = ToastType.Error,
                    }
                );
                vm.AvailableTemplates = (await _templateBudgetRepo.GetAllForUser(userId)).ToList();
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            int userId = GetCurrentUserId();
            var budget = await _budgetRepo.GetById(id);
            if (budget == null)
                return NotFound();
            if (!await HasAccessAsync(budget, userId))
                return Forbid();

            return View(MapToViewModel(budget));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(BudgetFormViewModel vm)
        {
            int userId = GetCurrentUserId();
            var existing = await _budgetRepo.GetById(vm.Id);
            if (existing == null)
                return NotFound();
            if (!await HasAccessAsync(existing, userId))
                return Forbid();

            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                var budget = MapToModel(vm);
                budget.Id = existing.Id;
                budget.CreatedByUserId = existing.CreatedByUserId;
                await _budgetRepo.Update(budget);

                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Erfolg",
                        Message = "Lagerdaten aktualisiert.",
                        Type = ToastType.Success,
                    }
                );
                return RedirectToAction(nameof(Detail), new { id = vm.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Edit");
                TempData.Put(
                    "ToastMsg",
                    new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Ein unerwarteter Fehler ist aufgetreten.",
                        Type = ToastType.Error,
                    }
                );
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            int userId = GetCurrentUserId();
            var budget = await _budgetRepo.GetById(id);
            if (budget == null)
                return NotFound();
            if (budget.CreatedByUserId != userId)
                return Forbid();

            var toast = new ToastMessageViewModel();
            try
            {
                await _positionRepo.DeleteByBudgetId(id);
                await _budgetRepo.Delete(id);
                toast = new ToastMessageViewModel
                {
                    Title = "Erfolg",
                    Message = "Budget gelöscht.",
                    Type = ToastType.Success,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Delete");
                toast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Ein unerwarteter Fehler ist aufgetreten.",
                    Type = ToastType.Error,
                };
            }
            TempData.Put("ToastMsg", toast);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            int userId = GetCurrentUserId();
            var budget = await _budgetRepo.GetById(id);
            if (budget == null)
                return NotFound();
            if (!await HasAccessAsync(budget, userId))
                return Forbid();

            var budgetUsers = (await _budgetUserRepo.GetByBudgetId(id)).ToList();

            var positions = (await _positionRepo.GetByBudgetId(id)).ToList();
            var categories = (await _categoryRepo.GetAll()).ToDictionary(c => c.Id);
            var subCategories = (await _subCategoryRepo.GetAll()).ToDictionary(sc => sc.Id);
            var positionTypes = (await _positionTypeRepo.GetAll()).ToDictionary(pt => pt.Id);

            var groups = positions
                .GroupBy(p => p.CategoryId)
                .Select(g =>
                {
                    categories.TryGetValue(g.Key, out var category);
                    return new CategoryGroupViewModel
                    {
                        Category = category!,
                        SubGroups = g.GroupBy(p => p.SubCategoryId)
                            .Select(sg =>
                            {
                                SubCategoryModel? subCategory = null;
                                if (sg.Key.HasValue)
                                    subCategories.TryGetValue(sg.Key.Value, out subCategory);
                                return new SubCategoryGroupViewModel
                                {
                                    SubCategory = subCategory,
                                    Positions = sg.OrderBy(p => p.SortIndex)
                                        .Select(p =>
                                        {
                                            positionTypes.TryGetValue(p.PositionTypeId, out var pt);
                                            return new PositionRowViewModel
                                            {
                                                Id = p.Id,
                                                Name = p.Name,
                                                PositionTypeName = pt?.Name ?? string.Empty,
                                                FixedAmount_fc = p.FixedAmount_fc,
                                                Quantity_fc = p.QuantityVar_fc switch
                                                {
                                                    "ParticipantsCount_fc" => (decimal)
                                                        budget.ParticipantsCount_fc,
                                                    "js_PersonsCount_fc" => (decimal)
                                                        budget.js_PersonsCount_fc,
                                                    "LeadersTeamCount_fc" => (decimal)
                                                        budget.LeadersTeamCount_fc,
                                                    _ => p.Quantity_fc,
                                                },
                                                UnitAmount_fc = p.UnitAmount_fc,
                                                FixedAmount_rl = p.FixedAmount_rl,
                                                Quantity_rl = p.QuantityVar_rl switch
                                                {
                                                    "ParticipantsCount_rl" => (decimal?)(
                                                        budget.ParticipantsCount_rl ?? 0
                                                    ),
                                                    "js_PersonsCount_rl" => (decimal?)(
                                                        budget.js_PersonsCount_rl ?? 0
                                                    ),
                                                    "LeadersTeamCount_rl" => (decimal?)(
                                                        budget.LeadersTeamCount_rl ?? 0
                                                    ),
                                                    _ => p.Quantity_rl,
                                                },
                                                UnitAmount_rl = p.UnitAmount_rl,
                                                QuantityVar_fc = p.QuantityVar_fc,
                                                QuantityVar_rl = p.QuantityVar_rl,
                                            };
                                        })
                                        .ToList(),
                                };
                            })
                            .OrderBy(sg => sg.SubCategory?.SortIndex ?? int.MaxValue)
                            .ToList(),
                    };
                })
                .OrderBy(g =>
                    categories.TryGetValue(g.Category.Id, out var cat) ? cat.SortIndex : 0
                )
                .ToList();

            bool isMainLeader = budgetUsers.IsMainLeaderFor(userId);

            var vm = new BudgetDetailViewModel
            {
                Budget = budget,
                BudgetUsers = budgetUsers,
                Groups = groups,
                PositionTypes = positionTypes.Values.OrderBy(pt => pt.Name).ToList(),
                AllCategories = categories.Values.OrderBy(c => c.SortIndex).ToList(),
                AllSubCategories = subCategories.Values.OrderBy(sc => sc.SortIndex).ToList(),
                IsMainLeader = isMainLeader,
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Leaders(int id)
        {
            int userId = GetCurrentUserId();
            var budget = await _budgetRepo.GetById(id);
            if (budget == null)
                return NotFound();
            if (!await HasAccessAsync(budget, userId))
                return Forbid();

            var budgetUsers = (await _budgetUserRepo.GetByBudgetId(id)).ToList();
            bool isMainLeader = budgetUsers.IsMainLeaderFor(userId);
            if (isMainLeader)
            {
                // Only the creator gets to see (and reactivate) deactivated leaders.
                budgetUsers = (
                    await _budgetUserRepo.GetByBudgetId(id, includeInactive: true)
                ).ToList();
            }
            var invites = (await _inviteRepo.GetByBudgetId(id)).ToList();

            var vm = new BudgetLeadersViewModel
            {
                Budget = budget,
                BudgetUsers = budgetUsers,
                Invites = invites,
                IsMainLeader = isMainLeader,
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> EditPositionModal(int id)
        {
            var pos = await _positionRepo.GetById(id);
            if (pos == null)
                return NotFound();

            var budget = await _budgetRepo.GetById(pos.BudgetId);
            if (budget == null)
                return NotFound();

            var positionTypes = (await _positionTypeRepo.GetAll()).OrderBy(pt => pt.Name).ToList();
            var categories = (await _categoryRepo.GetAll()).OrderBy(c => c.SortIndex).ToList();
            var subCategories = (await _subCategoryRepo.GetAll())
                .OrderBy(sc => sc.SortIndex)
                .ToList();

            var vm = new EditBudgetPositionViewModel
            {
                Id = pos.Id,
                BudgetId = pos.BudgetId,
                Name = pos.Name,
                PositionTypeId = pos.PositionTypeId,
                CategoryId = pos.CategoryId,
                SubCategoryId = pos.SubCategoryId,
                FixedAmount_fc = pos.FixedAmount_fc,
                QuantityVar_fc = pos.QuantityVar_fc,
                Quantity_fc = pos.Quantity_fc,
                UnitAmount_fc = pos.UnitAmount_fc,
                FixedAmount_rl = pos.FixedAmount_rl,
                QuantityVar_rl = pos.QuantityVar_rl,
                Quantity_rl = pos.Quantity_rl,
                UnitAmount_rl = pos.UnitAmount_rl,
                PositionTypes = positionTypes,
                Categories = categories,
                SubCategories = subCategories,
                CampParticipantsCount_fc = budget.ParticipantsCount_fc,
                CampJs_PersonsCount_fc = budget.js_PersonsCount_fc,
                CampLeadersTeamCount_fc = budget.LeadersTeamCount_fc,
                CampParticipantsCount_rl = budget.ParticipantsCount_rl,
                CampJs_PersonsCount_rl = budget.js_PersonsCount_rl,
                CampLeadersTeamCount_rl = budget.LeadersTeamCount_rl,
            };
            return PartialView("_EditPositionModal", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePosition(EditBudgetPositionViewModel vm)
        {
            var toast = new ToastMessageViewModel();
            try
            {
                var pos = await _positionRepo.GetById(vm.Id);
                if (pos != null)
                {
                    pos.Name = vm.Name;
                    pos.PositionTypeId = vm.PositionTypeId;
                    pos.CategoryId = vm.CategoryId;
                    pos.SubCategoryId = vm.SubCategoryId == 0 ? null : vm.SubCategoryId;
                    pos.FixedAmount_fc = vm.FixedAmount_fc ?? 0;
                    pos.QuantityVar_fc = string.IsNullOrEmpty(vm.QuantityVar_fc)
                        ? null
                        : vm.QuantityVar_fc;
                    pos.Quantity_fc = string.IsNullOrEmpty(vm.QuantityVar_fc)
                        ? (vm.Quantity_fc ?? 0)
                        : 0m;
                    pos.UnitAmount_fc = vm.UnitAmount_fc ?? 0;
                    pos.FixedAmount_rl = vm.FixedAmount_rl;
                    pos.QuantityVar_rl = string.IsNullOrEmpty(vm.QuantityVar_rl)
                        ? null
                        : vm.QuantityVar_rl;
                    pos.Quantity_rl = string.IsNullOrEmpty(vm.QuantityVar_rl) ? vm.Quantity_rl : 0m;
                    pos.UnitAmount_rl = vm.UnitAmount_rl;
                    await _positionRepo.Update(pos);
                }
                toast = new ToastMessageViewModel
                {
                    Title = "Erfolg",
                    Message = "Position gespeichert.",
                    Type = ToastType.Success,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SavePosition");
                toast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Ein unerwarteter Fehler ist aufgetreten.",
                    Type = ToastType.Error,
                };
            }
            TempData.Put("ToastMsg", toast);
            return RedirectToAction(nameof(Detail), new { id = vm.BudgetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBudgetPosition(AddBudgetPositionViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var invalidToast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Bitte alle Pflichtfelder ausfüllen.",
                    Type = ToastType.Error,
                };
                TempData.Put("ToastMsg", invalidToast);
                return RedirectToAction(nameof(Detail), new { id = vm.BudgetId });
            }

            try
            {
                var existing = await _positionRepo.GetByBudgetId(vm.BudgetId);
                int nextSortIndex = (existing.Any() ? existing.Max(p => p.SortIndex) : 0) + 1;

                var position = new PositionModel
                {
                    BudgetId = vm.BudgetId,
                    PositionTypeId = vm.PositionTypeId,
                    CategoryId = vm.CategoryId,
                    SubCategoryId = vm.SubCategoryId == 0 ? null : vm.SubCategoryId,
                    Name = vm.Name,
                    FixedAmount_fc = vm.FixedAmount,
                    QuantityVar_fc = string.IsNullOrEmpty(vm.QuantityVar) ? null : vm.QuantityVar,
                    Quantity_fc = string.IsNullOrEmpty(vm.QuantityVar) ? vm.Quantity : 0m,
                    UnitAmount_fc = vm.UnitAmount,
                    SortIndex = nextSortIndex,
                };
                await _positionRepo.Create(position);
                var toast = new ToastMessageViewModel
                {
                    Title = "Erfolg",
                    Message = "Position hinzugefügt.",
                    Type = ToastType.Success,
                };
                TempData.Put("ToastMsg", toast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddBudgetPosition");
                var toast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Ein unerwarteter Fehler ist aufgetreten.",
                    Type = ToastType.Error,
                };
                TempData.Put("ToastMsg", toast);
            }
            return RedirectToAction(nameof(Detail), new { id = vm.BudgetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBudgetPosition(int id, int budgetId)
        {
            var toast = new ToastMessageViewModel();
            try
            {
                await _positionRepo.Delete(id);
                toast = new ToastMessageViewModel
                {
                    Title = "Erfolg",
                    Message = "Position gelöscht.",
                    Type = ToastType.Success,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteBudgetPosition");
                toast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Ein unerwarteter Fehler ist aufgetreten.",
                    Type = ToastType.Error,
                };
            }
            TempData.Put("ToastMsg", toast);
            return RedirectToAction(nameof(Detail), new { id = budgetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateBudgetUser(int budgetUserId, int budgetId)
        {
            var toast = new ToastMessageViewModel();
            try
            {
                var budget = await _budgetRepo.GetById(budgetId);
                if (budget == null || budget.CreatedByUserId != GetCurrentUserId())
                {
                    toast = new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Keine Berechtigung.",
                        Type = ToastType.Error,
                    };
                    TempData.Put("ToastMsg", toast);
                    return RedirectToAction(nameof(Leaders), new { id = budgetId });
                }

                await _budgetUserRepo.Deactivate(budgetUserId, GetCurrentUserId());
                toast = new ToastMessageViewModel
                {
                    Title = "Erfolg",
                    Message = "Leitperson deaktiviert.",
                    Type = ToastType.Success,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeactivateBudgetUser");
                toast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Ein unerwarteter Fehler ist aufgetreten.",
                    Type = ToastType.Error,
                };
            }
            TempData.Put("ToastMsg", toast);
            return RedirectToAction(nameof(Leaders), new { id = budgetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateBudgetUser(int budgetUserId, int budgetId)
        {
            var toast = new ToastMessageViewModel();
            try
            {
                var budget = await _budgetRepo.GetById(budgetId);
                if (budget == null || budget.CreatedByUserId != GetCurrentUserId())
                {
                    toast = new ToastMessageViewModel
                    {
                        Title = "Fehler",
                        Message = "Keine Berechtigung.",
                        Type = ToastType.Error,
                    };
                    TempData.Put("ToastMsg", toast);
                    return RedirectToAction(nameof(Leaders), new { id = budgetId });
                }

                await _budgetUserRepo.Reactivate(budgetUserId, GetCurrentUserId());
                toast = new ToastMessageViewModel
                {
                    Title = "Erfolg",
                    Message = "Leitperson reaktiviert.",
                    Type = ToastType.Success,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReactivateBudgetUser");
                toast = new ToastMessageViewModel
                {
                    Title = "Fehler",
                    Message = "Ein unerwarteter Fehler ist aufgetreten.",
                    Type = ToastType.Error,
                };
            }
            TempData.Put("ToastMsg", toast);
            return RedirectToAction(nameof(Leaders), new { id = budgetId });
        }

        #region Helpers
        private static BudgetModel MapToModel(BudgetFormViewModel vm)
        {
            return new BudgetModel
            {
                Id = vm.Id,
                Name = vm.Name,
                Description = vm.Description,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate,
                MainLeader = vm.MainLeader,
                ParticipantsCount_fc = vm.ParticipantsCount_fc ?? 0,
                js_PersonsCount_fc = vm.js_PersonsCount_fc ?? 0,
                LeadersTeamCount_fc = vm.LeadersTeamCount_fc ?? 0,
                ParticipantsCount_rl = vm.ParticipantsCount_rl,
                js_PersonsCount_rl = vm.js_PersonsCount_rl,
                LeadersTeamCount_rl = vm.LeadersTeamCount_rl,
            };
        }

        private static BudgetFormViewModel MapToViewModel(BudgetModel budget)
        {
            return new BudgetFormViewModel
            {
                Id = budget.Id,
                Name = budget.Name,
                Description = budget.Description,
                StartDate = budget.StartDate,
                EndDate = budget.EndDate,
                MainLeader = budget.MainLeader,
                ParticipantsCount_fc = budget.ParticipantsCount_fc,
                js_PersonsCount_fc = budget.js_PersonsCount_fc,
                LeadersTeamCount_fc = budget.LeadersTeamCount_fc,
                ParticipantsCount_rl = budget.ParticipantsCount_rl,
                js_PersonsCount_rl = budget.js_PersonsCount_rl,
                LeadersTeamCount_rl = budget.LeadersTeamCount_rl,
            };
        }
        #endregion
    }
}
