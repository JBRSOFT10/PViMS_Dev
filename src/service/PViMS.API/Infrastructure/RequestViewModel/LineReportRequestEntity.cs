namespace PVIMS.API.Infrastructure.RequestViewModel
{
    public class LineReportRequestEntity
    {
        public string SearchFrom { get; set; }
        public string SearchTo { get; set; }
        public string PageNumber { get; set; }
        public string PageSize { get; set; }
    }
}
