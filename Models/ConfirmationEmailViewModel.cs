namespace BudgetApp.Models
{
    public class ConfirmationEmailViewModel
    {
        public required string DisplayName { get; set; }
        public required string ConfirmUrl { get; set; }
    }
}
