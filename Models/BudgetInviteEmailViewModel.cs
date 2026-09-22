namespace BudgetApp.Models
{
    public class BudgetInviteEmailViewModel
    {
        public required string DisplayName { get; set; }
        public required string BudgetName { get; set; }
        public required string InviteUrl { get; set; }
    }
}
