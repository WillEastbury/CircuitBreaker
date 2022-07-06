namespace CircuitBreaker.Custom.DTO
{
    public class StockUpdate 
    {
        public string StockCode { get; set; }
        public string StockLocation {get;set;}
        public int NewStockLevel { get; set; }
    }
}