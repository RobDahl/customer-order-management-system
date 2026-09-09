namespace Coms.Domain.Customers
{
    public sealed class CustomerFilter
    {
        /// <summary>Matches the start of the customer number, name or email.</summary>
        public string? Search { get; set; }

        public CustomerStatus? Status { get; set; }
    }
}
