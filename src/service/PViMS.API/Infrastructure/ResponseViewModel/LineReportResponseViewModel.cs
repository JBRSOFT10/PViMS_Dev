using Newtonsoft.Json;
using System.Collections.Generic;

namespace PVIMS.API.Infrastructure.ResponseViewModel
{
    public class LineReportResponseViewModel
    {
        public int pageCount { get; set; }
        public int recordCount { get; set; }
        public List<LinkList> links { get; set; } = new List<LinkList>();
        public List<Datum> value { get; set; }=new List<Datum>();
        public class Datum
        {

            public string? reportInstanceId { get; set; }
            public string? age { get; set; }
            public string? gender { get; set; }
            public string? genericNameWithStrength { get; set; }                         
            public string? indication { get; set; }
            public string? medicationstartdate { get; set; }
            public string? frequencyDailyDose { get; set; }
            public string? describeeventincludingrelevanttestsandlaboratoryresults { get; set; }
            public string? eventstartdate { get; set; }
            public string? slno { get; set; }
            public string? patientIdentifier { get; set; }
            public string? dosageFrom { get; set; } = "N/A";
            public string? adrmStatus { get; set; } = string.Empty;
            public string? opinionTsc { get; set; } = string.Empty;
            public string? opinionAdrac { get; set; } = string.Empty;
            public string? submissionDate { get; set; }
        }
        public class LinkList
        {
        }

    }


}
