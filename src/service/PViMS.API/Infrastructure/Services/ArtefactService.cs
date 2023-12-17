using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PVIMS.Core.CustomAttributes;
using PVIMS.Core.Entities;
using PVIMS.Core.Models;
using PVIMS.Core.Repositories;
using PVIMS.Core.Services;
using PVIMS.Core.Aggregates.DatasetAggregate;
using PVIMS.Core.Aggregates.ReportInstanceAggregate;
using System.Linq;
using PVIMS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Data;
using PVIMS.API.Infrastructure.PdfUtility;

namespace PVIMS.API.Infrastructure.Services
{
    public class ArtefactService : IArtefactService
    {
        ArtefactInfoModel _fileModel;
        private readonly IRepositoryInt<DatasetElement> _datasetElementRepository;
        private readonly IRepositoryInt<DatasetInstance> _datasetInstanceRepository;
        private readonly IRepositoryInt<PatientClinicalEvent> _patientClinicalEventRepository;
        private readonly IRepositoryInt<PatientMedication> _patientMedicationRepository;
        private readonly IRepositoryInt<ReportInstance> _reportInstanceRepository;
        private readonly ICustomAttributeService _attributeService;
        private readonly IPatientService _patientService;
        private readonly IWordDocumentService _wordDocumentService;
        private readonly PVIMSDbContext _dbContext;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;
        public ArtefactService(
            IRepositoryInt<DatasetElement> datasetElementRepository,
            IRepositoryInt<DatasetInstance> datasetInstanceRepository,
            IRepositoryInt<PatientClinicalEvent> patientClinicalEventRepository,
            IRepositoryInt<PatientMedication> patientMedicationRepository,
            IRepositoryInt<ReportInstance> reportInstanceRepository,
            ICustomAttributeService attributeService,
            IPatientService patientService,
            IWordDocumentService wordDocumentService,
            PVIMSDbContext dbContext, IWebHostEnvironment hostingEnvironment, IConfiguration configuration)
        {
            _datasetElementRepository = datasetElementRepository ?? throw new ArgumentNullException(nameof(datasetElementRepository));
            _datasetInstanceRepository = datasetInstanceRepository ?? throw new ArgumentNullException(nameof(datasetInstanceRepository));
            _patientClinicalEventRepository = patientClinicalEventRepository ?? throw new ArgumentNullException(nameof(patientClinicalEventRepository));
            _patientMedicationRepository = patientMedicationRepository ?? throw new ArgumentNullException(nameof(patientMedicationRepository));
            _reportInstanceRepository = reportInstanceRepository ?? throw new ArgumentNullException(nameof(reportInstanceRepository));
            _attributeService = attributeService ?? throw new ArgumentNullException(nameof(attributeService));
            _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));
            _wordDocumentService = wordDocumentService ?? throw new ArgumentNullException(nameof(wordDocumentService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<ArtefactInfoModel> CreatePatientSummaryForActiveReportAsync(Guid contextGuid)
        {
            var patientClinicalEvent = await GetPatientClinicalEventAsync(contextGuid);
            var reportInstance = await GetReportInstanceForPatientClinicalEventAsync(contextGuid);

            var isSerious = await CheckIfSeriousAsync(patientClinicalEvent);
            var model = PrepareFileModelForActive(patientClinicalEvent, isSerious);

            _wordDocumentService.CreateDocument(model);
            _wordDocumentService.AddPageHeader(isSerious ? "SERIOUS ADVERSE EVENT" : "PATIENT SUMMARY");

            _wordDocumentService.AddTableHeader("A. BASIC PATIENT INFORMATION");
            _wordDocumentService.AddFourColumnTable(await PrepareBasicInformationForActiveReportAsync(patientClinicalEvent));
            _wordDocumentService.AddTwoColumnTable(PrepareNotes(patientClinicalEvent.Patient.Notes));
            _wordDocumentService.AddTableHeader("B. PRE-EXISITING CONDITIONS");
            _wordDocumentService.AddTwoColumnTable(PrepareConditionsForActiveReport(patientClinicalEvent));
            _wordDocumentService.AddTableHeader("C. ADVERSE EVENT INFORMATION");
            _wordDocumentService.AddFourColumnTable(await PrepareAdverseEventForActiveReportAsync(patientClinicalEvent, reportInstance, isSerious));
            _wordDocumentService.AddTwoColumnTable(PrepareNotes(""));
            _wordDocumentService.AddTableHeader("D. MEDICATIONS");
            await PrepareMedicationsForActiveReportAsync(reportInstance);
            _wordDocumentService.AddTableHeader("E. CLINICAL EVALUATIONS");
            PrepareEvaluationsForActiveReport(patientClinicalEvent);
            _wordDocumentService.AddTwoColumnTable(PrepareNotes(""));

            //_wordDocumentService.AddTableHeader("F. WEIGHT HISTORY");
            //PrepareWeightForActiveReport(patientClinicalEvent);

            //_wordDocumentService.AddTwoColumnTable(PrepareNotes(""));

            return model;
        }

        public async Task<ArtefactInfoModel> CreatePatientSummaryForSpontaneousReportAsync(Guid contextGuid, string patientIdentifier="")
        {
            string pdf_download_path = string.Empty;
            string pdf_path = string.Empty;
            StringBuilder result = new();

            var datasetInstance = await GetDatasetInstanceAsync(contextGuid);

            var isSerious = !String.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("Reaction serious details"));
            var model = PrepareFileModelForSpontaneous(datasetInstance, isSerious);
            _fileModel = model;
            string wwwPath = _hostingEnvironment.WebRootPath;
            var html_template_path = _configuration.GetValue<string>("FilePaths:adverse_event_report_template_path");
            var html_template_full_path = Path.Combine(wwwPath, html_template_path);
            result = new(System.IO.File.ReadAllText(html_template_full_path));


            var reportInstance = await _reportInstanceRepository.GetAsync(ri => ri.ContextGuid == datasetInstance.DatasetInstanceGuid, new string[] { "Medications" });
            if (reportInstance == null) { return null; };

            var sourceProductElement = await _datasetElementRepository.GetAsync(dse => dse.ElementName == "Concomitant Medicine Information");


            DataTable dynamic_data = CommonService.GenerateDynamicData(sourceProductElement);
            string dynamic_data_html = CommonHelpers.ConvertDataTableToHTMLWithCustomClass(dynamic_data, true, 100, 12);
            result.Replace("{tbl_product}", dynamic_data_html);

            result.Replace("{ae_report_number}", patientIdentifier);
            result.Replace("{date_received}", DateTime.TryParse(datasetInstance.GetInstanceValue("Date Received"), out var dateOfBirthValue) ? dateOfBirthValue.ToString("yyyy-MM-dd") : "");

            result.Replace("{name_or_initial}", datasetInstance.GetInstanceValue("Patient Name"));
            result.Replace("{contact_number}", datasetInstance.GetInstanceValue("Patient Contact"));
            result.Replace("{age}", $"{datasetInstance.GetInstanceValue("Age")} y {datasetInstance.GetInstanceValue("Age Month")} m {datasetInstance.GetInstanceValue("Age Day")} d ");
            result.Replace("{weight}", datasetInstance.GetInstanceValue("Weight  (kg)"));
            result.Replace("{gender}", datasetInstance.GetInstanceValue("Gender"));
            result.Replace("{pregnant}", datasetInstance.GetInstanceValue("Pregnant"));
            result.Replace("{address}", datasetInstance.GetInstanceValue("Address"));

            if (!string.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("Division")))
            {
                var query = _dbContext.OrgUnits.Where(x => x.Id == Convert.ToInt32(datasetInstance.GetInstanceValue("Division"))).ToList();
                foreach (var item in query)
                {
                    result.Replace("{division}", item.Name);
                }

            }
            else
            {
                result.Replace("{division}", String.Empty);
            }
            if (!string.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("District")))
            {
                var query = _dbContext.OrgUnits.Where(x => x.Id == Convert.ToInt32(datasetInstance.GetInstanceValue("District"))).ToList();
                foreach (var item in query)
                {
                    result.Replace("{district}", item.Name);
                }
            }
            else
            {
                result.Replace("{district}", String.Empty);

            }
            if (!string.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("Thana")))
            {
                var query = _dbContext.OrgUnits.Where(x => x.Id == Convert.ToInt32(datasetInstance.GetInstanceValue("Thana"))).ToList();
                foreach (var item in query)
                {
                    result.Replace("{thana}", item.Name);
                }
            }
            else
            {
                result.Replace("{thana}", String.Empty);

            }

            result.Replace("{reporter_name_and_address}", datasetInstance.GetInstanceValue("Reporter Name")+" , "+ datasetInstance.GetInstanceValue("Reporter Address"));
            result.Replace("{reporter_mobile_phone}", datasetInstance.GetInstanceValue("Reporter Mobile Number"));
            result.Replace("{reporter_email_address}", datasetInstance.GetInstanceValue("Reporter E-mail Address"));
            result.Replace("{reporter_occupation}", datasetInstance.GetInstanceValue("Reporter Profession"));
            result.Replace("{organization}", datasetInstance.GetInstanceValue("Organization"));
            result.Replace("{date_of_report_submission}", DateTime.TryParse(datasetInstance.GetInstanceValue("Date of Report Submission"), out var dateofReportSubmission) ? dateofReportSubmission.ToString("yyyy-MM-dd") : "");
            result.Replace("{signature}", String.Empty);



            result.Replace("{type_of_event}", datasetInstance.GetInstanceValue("Type of event") ?? String.Empty);
            result.Replace("{brand_or_trade_name}", datasetInstance.GetInstanceValue("Brand/Trade Name"));
            result.Replace("{generic_name_with_strength}", datasetInstance.GetInstanceValue("Generic Name With Strength"));
            result.Replace("{indication}", datasetInstance.GetInstanceValue("Indication"));
            result.Replace("{medication_start_date}", DateTime.TryParse(datasetInstance.GetInstanceValue("Medication start date"), out var dateOfMedicationStartDateValue) ? dateOfMedicationStartDateValue.ToString("yyyy-MM-dd") : "");
            result.Replace("{medication_end_date}", DateTime.TryParse(datasetInstance.GetInstanceValue("Medication end date"), out var dateOfMedicationEndDateValue) ? dateOfMedicationEndDateValue.ToString("yyyy-MM-dd") : "");
            result.Replace("{dosage_form}", datasetInstance.GetInstanceValue("{dosage_form}"));
            result.Replace("{frequency_daily_dose}", datasetInstance.GetInstanceValue("Frequency(Daily Dose)"));
            result.Replace("{batch_or_lot_number}", datasetInstance.GetInstanceValue("Batch/Lot Number"));
            result.Replace("{manufacturer}", datasetInstance.GetInstanceValue("Manufacturer"));
            result.Replace("{event_description}", datasetInstance.GetInstanceValue("Describe event including relevant tests and laboratory results") ?? String.Empty);
            result.Replace("{event_start_date}", DateTime.TryParse(datasetInstance.GetInstanceValue("Event start date"), out var dateOfEventStartDateValue) ? dateOfEventStartDateValue.ToString("yyyy-MM-dd") : "");
            result.Replace("{event_stopped_date}", DateTime.TryParse(datasetInstance.GetInstanceValue("Event end date"), out var dateOfEventEndDateValue) ? dateOfEventEndDateValue.ToString("yyyy-MM-dd") : "");
            result.Replace("{was_the_adverse_event_treated}", datasetInstance.GetInstanceValue("Was the adverse event treated?"));
            result.Replace("{adverse_event_treatment_description}", datasetInstance.GetInstanceValue("If Yes, Please Specify"));
            result.Replace("{action_taken_after_reaction}", datasetInstance.GetInstanceValue("Action taken after reaction"));
            result.Replace("{reaction_subside_stopping_or_reducing_dose}", datasetInstance.GetInstanceValue("Did reaction subside after stopping/reducing the dose of the suspected products?"));
            result.Replace("{reaction_appear_reintroducing_suspected_product}", datasetInstance.GetInstanceValue("Did reaction appear after reintroducing the suspected products?"));
            result.Replace("{seriousness_of_the_adverse_event}", datasetInstance.GetInstanceValue("Seriousness of the adverse event"));
            result.Replace("{outcome_attributed_to_the_adverse_event}", datasetInstance.GetInstanceValue("Outcomes attributed to the adverse event"));
            result.Replace("{date_of_death}", datasetInstance.GetInstanceValue("Date of death"));
            result.Replace("{other_relevant_history}", datasetInstance.GetInstanceValue("Other relevant history"));
            result.Replace("{other_description}", datasetInstance.GetInstanceValue("If Others, Please Specify"));
            result.Replace("{typeofevent_description}", datasetInstance.GetInstanceValue("Others, Please Specify Comments"));
            result.Replace("{diluent_information}", datasetInstance.GetInstanceValue("Diluent information for vaccine"));
            result.Replace("{please_specify_event_info}", datasetInstance.GetInstanceValue("Please Specify (if select others on event information)"));

            if (!string.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("Report Type")))
            {
                result.Replace("{report_type}", datasetInstance.GetInstanceValue("Report Type"));
            }
            else
            {
                result.Replace("{report_type}", "Initial report");
            }
            result.Replace("{initial_report_id}", datasetInstance.GetInstanceValue("Initial Report Id"));
            result.Replace("{company_name}", datasetInstance.GetInstanceValue("Company Name"));
            result.Replace("{reporting_source}", datasetInstance.GetInstanceValue("Enter source of reporting"));
            result.Replace("{event_information}", datasetInstance.GetInstanceValue("Event Information"));
            result.Replace("{dosage}", datasetInstance.GetInstanceValue("Dosage Form"));
            
            string report_save_path = _configuration.GetValue<string>("FilePaths:report_save_path");
            string report_save_full_path = Path.Combine(wwwPath, report_save_path);
            string font_path = _configuration.GetValue<string>("FONT_DIRECTORY:SEGOE_UI");
            pdf_path = CommonHelpers.GeneratePDFFromHTMLWithCustomFont($"{DateTime.Now:yyyyMMddhhmmss}.html", report_save_full_path, result, wwwPath, font_path);
            File.WriteAllText(pdf_path, result.ToString());

            pdf_download_path = $"{report_save_path}{Path.GetFileName(pdf_path)}";
            model.Path = string.Empty;
            model.FileName = pdf_path;

            //_wordDocumentService.CreateDocument(model);
            //_wordDocumentService.AddPageHeader(isSerious ? "SERIOUS ADVERSE EVENT" : "YELLOW CARD");
            //_wordDocumentService.AddPageHeader("SUSPECTED ADVERSE EVENT REPORTING FORM");
            //_wordDocumentService.AddPageHeader("Identities of reporter , patient , institution and product trade name(s) will remain confidential");

            //_wordDocumentService.AddTableHeader("FOR OFFICE USE ONLY");
            //_wordDocumentService.AddFourColumnTable(PrepareOfficeUseForSpontaneousReport(datasetInstance, patientIdentifier));


            //_wordDocumentService.AddTableHeader("A.PATIENT INFORMATION");
            //_wordDocumentService.AddFourColumnTable(PrepareBasicInformationForSpontaneousReport(datasetInstance));


            //_wordDocumentService.AddTableHeader("B. SUSPECTED DRUG/VACCINE INFORMATION");
            //_wordDocumentService.AddFourColumnTable(PrepareSuspectedBasicInformationForSpontaneousReport(datasetInstance));


            //_wordDocumentService.AddTableHeader("C. OTHER CONCOMITANT PRODUCT INFORMATION");
            //await PrepareMedicineInformationForSpontaneousReportAsync(datasetInstance);

            //_wordDocumentService.AddTableHeader("D. REPORTER INFORMATION");
            //_wordDocumentService.AddFourColumnTable(PrepareReporterInformationForSpontaneousReport(datasetInstance));


            #region XCode

            //_wordDocumentService.AddTwoColumnTable(PrepareNotes(""));

            //_wordDocumentService.AddTableHeader("B. ADVERSE EVENT INFORMATION");
            //_wordDocumentService.AddFourColumnTable(await PrepareAdverseEventForSpontaneousReportAsync(datasetInstance, isSerious));
            //_wordDocumentService.AddTwoColumnTable(PrepareNotes(""));
            //_wordDocumentService.AddTableHeader("C. MEDICATIONS");

            //await PrepareMedicationsForSpontaneousReportAsync(datasetInstance);

            //_wordDocumentService.AddTableHeader("D. CLINICAL EVALUATIONS");

            //await PrepareEvaluationsForSpontaneousReport(datasetInstance);

            //_wordDocumentService.AddTwoColumnTable(PrepareNotes(""));
            #endregion

            return model;
        }

        private async Task<bool> CheckIfSeriousAsync(PatientClinicalEvent patientClinicalEvent)
        {
            var extendable = (IExtendable)patientClinicalEvent;
            var extendableValue = await _attributeService.GetCustomAttributeValueAsync("PatientClinicalEvent", "Is the adverse event serious?", extendable);
            return extendableValue == "Yes";
        }

        private ArtefactInfoModel PrepareFileModelForActive(PatientClinicalEvent patientClinicalEvent, bool isSerious)
        {
            var model = new ArtefactInfoModel();
            var generatedDate = DateTime.Now.ToString("yyyyMMddhhmmss");

            model.Path = Path.GetTempPath();
            var fileNamePrefix = isSerious ? "SAEReport_Active" : "PatientSummary_Active";
            model.FileName = $"{fileNamePrefix}{patientClinicalEvent.Patient.Id}_{generatedDate}.docx";
            return model;
        }

        private ArtefactInfoModel PrepareFileModelForSpontaneous(DatasetInstance datasetInstance, bool isSerious)
        {
            var model = new ArtefactInfoModel();
            var generatedDate = DateTime.Now.ToString("yyyyMMddhhmmss");

            model.Path = Path.GetTempPath();
            var fileNamePrefix = isSerious ? "SAEReport_Spontaneous" : "PatientSummary_Spontaneous";
            //model.FileName = $"{fileNamePrefix}{datasetInstance.Id}_{generatedDate}.docx";
            model.FileName = $"{fileNamePrefix}{datasetInstance.Id}_{generatedDate}.html";
            return model;
        }

        private async Task<PatientClinicalEvent> GetPatientClinicalEventAsync(Guid contextGuid)
        {
            var patientClinicalEvent = await _patientClinicalEventRepository.GetAsync(pce => pce.PatientClinicalEventGuid == contextGuid,
                new string[] { "SourceTerminologyMedDra",
                    "Patient.PatientFacilities.Facility",
                    "Patient.PatientConditions.TerminologyMedDra", "Patient.PatientConditions.Outcome", "Patient.PatientConditions.TreatmentOutcome",
                    "Patient.PatientMedications.Concept.MedicationForm", "Patient.PatientMedications.Product",
                    "Patient.PatientLabTests.LabTest", "Patient.PatientLabTests.TestUnit",
                    "Patient.PatientFacilities.Facility"});
            if (patientClinicalEvent == null)
            {
                throw new KeyNotFoundException(nameof(patientClinicalEvent));
            }

            return patientClinicalEvent;
        }

        private async Task<ReportInstance> GetReportInstanceForPatientClinicalEventAsync(Guid contextGuid)
        {
            var reportInstance = await _reportInstanceRepository.GetAsync(ri => ri.ContextGuid == contextGuid, new string[] { "Medications",
                "TerminologyMedDra" });

            if (reportInstance == null)
            {
                throw new KeyNotFoundException(nameof(reportInstance));
            }

            return reportInstance;
        }

        private async Task<DatasetInstance> GetDatasetInstanceAsync(Guid contextGuid)
        {
            var datasetInstance = await _datasetInstanceRepository.GetAsync(di => di.DatasetInstanceGuid == contextGuid, new string[] { "DatasetInstanceValues.DatasetElement", "DatasetInstanceValues.DatasetInstanceSubValues.DatasetElementSub" });
            if (datasetInstance == null)
            {
                throw new KeyNotFoundException(nameof(datasetInstance));
            }

            return datasetInstance;
        }

        private async Task<List<KeyValuePair<string, string>>> PrepareBasicInformationForActiveReportAsync(PatientClinicalEvent patientClinicalEvent)
        {
            var extendable = (IExtendable)patientClinicalEvent;
            var patientExtendable = (IExtendable)patientClinicalEvent.Patient;
            List<KeyValuePair<string, string>> rows = new();

            rows.Add(new KeyValuePair<string, string>("Patient Name", patientClinicalEvent.Patient.FullName));
            rows.Add(new KeyValuePair<string, string>("Date of Birth (yyyy-mm-dd)", patientClinicalEvent.Patient.DateOfBirth.HasValue ? patientClinicalEvent.Patient.DateOfBirth.Value.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Age Group", patientClinicalEvent.Patient.AgeGroup));

            if (patientClinicalEvent.OnsetDate.HasValue && patientClinicalEvent.Patient.DateOfBirth.HasValue)
            {
                rows.Add(new KeyValuePair<string, string>("Age at time of onset", $"{CalculateAge(patientClinicalEvent.Patient.DateOfBirth.Value, patientClinicalEvent.OnsetDate.Value).ToString()} years"));
            }
            else
            {
                rows.Add(new KeyValuePair<string, string>("Age at time of onset", string.Empty));
            }

            rows.Add(new KeyValuePair<string, string>("Gender", await _attributeService.GetCustomAttributeValueAsync("Patient", "Gender", patientExtendable)));
            rows.Add(new KeyValuePair<string, string>("Ethnic Group", await _attributeService.GetCustomAttributeValueAsync("Patient", "Ethnic Group", patientExtendable)));
            rows.Add(new KeyValuePair<string, string>("Facility", patientClinicalEvent.Patient.CurrentFacility.Facility.FacilityName));
            rows.Add(new KeyValuePair<string, string>("Region", ""));
            //rows.Add(new KeyValuePair<string, string>("Medical Record Number", await _attributeService.GetCustomAttributeValueAsync("Patient", "Medical Record Number", extendable)));
            //rows.Add(new KeyValuePair<string, string>("Identity Number", await _attributeService.GetCustomAttributeValueAsync("Patient", "Patient Identity Number", extendable)));

            rows.Add(new KeyValuePair<string, string>("Weight (kg)", await _attributeService.GetCustomAttributeValueAsync("PatientClinicalEvent", "Weight (kg)", extendable)));
            rows.Add(new KeyValuePair<string, string>("Height (cm)", await _attributeService.GetCustomAttributeValueAsync("PatientClinicalEvent", "Height (cm)", extendable)));

            return rows;
        }


        #region MyRegion
        private List<KeyValuePair<string, string>> PrepareOfficeUseForSpontaneousReport(DatasetInstance datasetInstance,string patientIdentifier)
        {
            List<KeyValuePair<string, string>> rows = new();

            rows.Add(new KeyValuePair<string, string>("AE report number", patientIdentifier));
            rows.Add(new KeyValuePair<string, string>("Date received", DateTime.TryParse(datasetInstance.GetInstanceValue("Date Received"), out var dateOfBirthValue) ? dateOfBirthValue.ToString("yyyy-MM-dd") : ""));

            return rows;
        }
        public List<KeyValuePair<string, string>> PrepareBasicInformationForSpontaneousReport(DatasetInstance datasetInstance)
        {
            List<KeyValuePair<string, string>> rows = new();

            rows.Add(new KeyValuePair<string, string>("Name/Initial", datasetInstance.GetInstanceValue("Patient Name")));
            rows.Add(new KeyValuePair<string, string>("Contact Number", datasetInstance.GetInstanceValue("Patient Contact")));
            rows.Add(new KeyValuePair<string, string>("Age", $"{datasetInstance.GetInstanceValue("Age")} {datasetInstance.GetInstanceValue("Age Unit")}"));
            rows.Add(new KeyValuePair<string, string>("Weight(kg)", datasetInstance.GetInstanceValue("Weight  (kg)")));
            //rows.Add(new KeyValuePair<string, string>("Patient Contact", DateTime.TryParse(datasetInstance.GetInstanceValue("Date Received"), out var dateOfBirthValue) ? dateOfBirthValue.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Gender", datasetInstance.GetInstanceValue("Gender")));
            rows.Add(new KeyValuePair<string, string>("Pregnant", datasetInstance.GetInstanceValue("Pregnant")));
            rows.Add(new KeyValuePair<string, string>("Address", datasetInstance.GetInstanceValue("Address")));

            if (!string.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("Division")))
            {
                var query =_dbContext.OrgUnits.Where(x => x.Id ==Convert.ToInt32(datasetInstance.GetInstanceValue("Division"))).ToList();
                foreach (var item in query)
                {
                    rows.Add(new KeyValuePair<string, string>("Division", item.Name));
                }
                   
            }
            else
            {
                rows.Add(new KeyValuePair<string, string>("Division", String.Empty));
            }
            if (!string.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("District")))
            {
                var query = _dbContext.OrgUnits.Where(x => x.Id == Convert.ToInt32(datasetInstance.GetInstanceValue("District"))).ToList();
                foreach (var item in query)
                {
                    rows.Add(new KeyValuePair<string, string>("District", item.Name));
                }
            }
            else
            {
                rows.Add(new KeyValuePair<string, string>("District", String.Empty));

            }
            if (!string.IsNullOrWhiteSpace(datasetInstance.GetInstanceValue("Thana")))
            {
                var query = _dbContext.OrgUnits.Where(x => x.Id == Convert.ToInt32(datasetInstance.GetInstanceValue("Thana"))).ToList();
                foreach (var item in query)
                {
                    rows.Add(new KeyValuePair<string, string>("Thana", item.Name));
                }
            }
            else
            {
                rows.Add(new KeyValuePair<string, string>("Thana", String.Empty));

            }
            return rows;
        }
        private List<KeyValuePair<string, string>> PrepareReporterInformationForSpontaneousReport(DatasetInstance datasetInstance)
        {
            List<KeyValuePair<string, string>> rows = new();

            rows.Add(new KeyValuePair<string, string>("Name & address", datasetInstance.GetInstanceValue("Reporter Name")));
            rows.Add(new KeyValuePair<string, string>("Mobile phone", datasetInstance.GetInstanceValue("Reporter Mobile Number")));
            rows.Add(new KeyValuePair<string, string>("Email address", datasetInstance.GetInstanceValue("Reporter E-mail Address")));
            rows.Add(new KeyValuePair<string, string>("Occupation", datasetInstance.GetInstanceValue("Reporter Profession")));
            rows.Add(new KeyValuePair<string, string>("Organization", datasetInstance.GetInstanceValue("Organization")));
            rows.Add(new KeyValuePair<string, string>("Date of this report submission", DateTime.TryParse(datasetInstance.GetInstanceValue("Date of Report Submission"), out var dateofReportSubmission) ? dateofReportSubmission.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Signature", String.Empty));
            rows.Add(new KeyValuePair<string, string>("", String.Empty));
            return rows;
        }
        private List<KeyValuePair<string, string>> PrepareSuspectedBasicInformationForSpontaneousReport(DatasetInstance datasetInstance)
        {
            List<KeyValuePair<string, string>> rows = new();
            rows.Add(new KeyValuePair<string, string>("Type of event", datasetInstance.GetInstanceValue("Type of event") ?? String.Empty));
            rows.Add(new KeyValuePair<string, string>("Brand/Trade name", datasetInstance.GetInstanceValue("Brand/Trade Name")));
            rows.Add(new KeyValuePair<string, string>("Generic name with strength", datasetInstance.GetInstanceValue("Generic Name With Strength")));
            rows.Add(new KeyValuePair<string, string>("Indication", datasetInstance.GetInstanceValue("Indication")));
            rows.Add(new KeyValuePair<string, string>("Medication start date", DateTime.TryParse(datasetInstance.GetInstanceValue("Medication start date"), out var dateOfMedicationStartDateValue) ? dateOfMedicationStartDateValue.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Medication end date", DateTime.TryParse(datasetInstance.GetInstanceValue("Medication end date"), out var dateOfMedicationEndDateValue) ? dateOfMedicationEndDateValue.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Dosage Form", datasetInstance.GetInstanceValue("Dosage Form")));
            rows.Add(new KeyValuePair<string, string>("Frequency (Daily Dose)", datasetInstance.GetInstanceValue("Frequency(Daily Dose)")));
            rows.Add(new KeyValuePair<string, string>("Batch/Lot Number", datasetInstance.GetInstanceValue("Batch/Lot Number")));
            rows.Add(new KeyValuePair<string, string>("Manufacturer", datasetInstance.GetInstanceValue("Manufacturer")));
            rows.Add(new KeyValuePair<string, string>("Describe event including relevant tests and laboratory results", datasetInstance.GetInstanceValue("Describe event including relevant tests and laboratory results") ?? String.Empty));
            rows.Add(new KeyValuePair<string, string>("Event start date", DateTime.TryParse(datasetInstance.GetInstanceValue("Event start date"), out var dateOfEventStartDateValue) ? dateOfEventStartDateValue.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Event stopped date", DateTime.TryParse(datasetInstance.GetInstanceValue("Event end date"), out var dateOfEventEndDateValue) ? dateOfEventEndDateValue.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Was the adverse event treated?", datasetInstance.GetInstanceValue("Was the adverse event treated?")));
            rows.Add(new KeyValuePair<string, string>("If yes, please specify", datasetInstance.GetInstanceValue("If Yes, Please Specify")));
            rows.Add(new KeyValuePair<string, string>("Action taken after reaction", datasetInstance.GetInstanceValue("Action taken after reaction")));
            rows.Add(new KeyValuePair<string, string>("Did reaction subside after stopping/reducing the dose of the suspected products?", datasetInstance.GetInstanceValue("Did reaction subside after stopping/reducing the dose of the suspected products?")));
            rows.Add(new KeyValuePair<string, string>("Did reaction appear after reintroducing the suspected products?", datasetInstance.GetInstanceValue("Did reaction appear after reintroducing the suspected products?")));
            rows.Add(new KeyValuePair<string, string>("Seriousness of the adverse event", datasetInstance.GetInstanceValue("Seriousness of the adverse event")));
            rows.Add(new KeyValuePair<string, string>("Outcomes attributed to the adverse event", datasetInstance.GetInstanceValue("Outcomes attributed to the adverse event")));
            rows.Add(new KeyValuePair<string, string>("Fatal (date of death)", datasetInstance.GetInstanceValue("Date of death")));
            rows.Add(new KeyValuePair<string, string>("Other relevant history (pre-existing medical history)", datasetInstance.GetInstanceValue("Other relevant history")));
            rows.Add(new KeyValuePair<string, string>("If Others, Please Specify", datasetInstance.GetInstanceValue("If Others, Please Specify")));
            rows.Add(new KeyValuePair<string, string>("", String.Empty));

            return rows;
        }
        public class GUIIDCount
        {
            public List<string> gui_id { get; set; }
        }

        private async Task PrepareMedicineInformationForSpontaneousReportAsync(DatasetInstance datasetInstance)
        {
            var reportInstance = await _reportInstanceRepository.GetAsync(ri => ri.ContextGuid == datasetInstance.DatasetInstanceGuid, new string[] { "Medications" });
            if (reportInstance == null) { return; };

            var sourceProductElement = await _datasetElementRepository.GetAsync(dse => dse.ElementName == "Concomitant Medicine Information");
            var sourceContexts = datasetInstance.GetInstanceSubValuesContext(sourceProductElement.ElementName);
            var i = 0;
            List<string[]> rows = new();
            List<string> cells = new();

            cells.Add("Brand/Trade name");
            cells.Add("Generic name");
            cells.Add("Indication");
            cells.Add("Dosage form");
            cells.Add("Strength & Frequency");
            rows.Add(cells.ToArray());
        
            foreach (var item in sourceProductElement.DatasetInstanceValues)
            {
                i += 1;

                List<string> list = new List<string>();
                foreach (var data in item.DatasetInstanceSubValues)
                {

                    if (!list.Contains(data.ContextValue.ToString()))
                    {
                        list.Add(data.ContextValue.ToString()); 
                    }
                }


                foreach (var guiID in list)
                {
                    cells = new();

                    var brandValue = item.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Brand/Trade name" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var genericName = item.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Generic name" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var indicationValue = item.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Indication" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var dosageValue = item.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Dosage form" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var strengthValue = item.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Strength & Frequency" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);


                    cells.Add(brandValue != null ? brandValue.SingleOrDefault() : string.Empty);
                    cells.Add(genericName != null ? genericName.SingleOrDefault() : string.Empty);
                    cells.Add(indicationValue != null ? indicationValue.SingleOrDefault() : string.Empty);
                    cells.Add(dosageValue != null ? dosageValue.SingleOrDefault() : string.Empty);
                    cells.Add(strengthValue != null ? strengthValue.SingleOrDefault() : string.Empty);

                    rows.Add(cells.ToArray());
    
                }


            }
            _wordDocumentService.AddRowTable(rows, new int[] { 2500, 1250, 1250, 1250, 1250, 3852 });



            #region XCode
            //_wordDocumentService.AddTableHeader("C.2 EFFECT OF DECHALLENGE/ RECHALLENGE");
            //_wordDocumentService.AddOneColumnTable(new List<string>() { "" });
            //_wordDocumentService.AddTableHeader("C.3 NOTES");
            //_wordDocumentService.AddOneColumnTable(new List<string>() { "" });

            //foreach (ReportInstanceMedication med in reportInstance.Medications)
            //{
            //    i += 1;

            //    List<string[]> rows = new();
            //    List<string> cells = new();

            //    cells.Add($"Drug {i}");
            //    cells.Add("Start Date");
            //    cells.Add("End Date");
            //    cells.Add("Dose");
            //    cells.Add("Route");
            //    cells.Add("Indication");

            //    rows.Add(cells.ToArray());

            //    cells = new();

            //    var drugItemValues = datasetInstance.GetInstanceSubValues(sourceProductElement.ElementName, med.ReportInstanceMedicationGuid);

            //    var startValue = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug Start Date");
            //    var endValue = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug End Date");
            //    var dose = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Dose number");
            //    var route = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug route of administration");
            //    var indication = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug Indication");

            //    cells.Add(drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Product").InstanceValue);
            //    cells.Add(startValue != null ? Convert.ToDateTime(startValue.InstanceValue).ToString("yyyy-MM-dd") : string.Empty);
            //    cells.Add(endValue != null ? Convert.ToDateTime(endValue.InstanceValue).ToString("yyyy-MM-dd") : string.Empty);
            //    cells.Add(dose != null ? dose.InstanceValue : string.Empty);
            //    cells.Add(route != null ? route.InstanceValue : string.Empty);
            //    cells.Add(indication != null ? indication.InstanceValue : string.Empty);

            //    rows.Add(cells.ToArray());

            //    _wordDocumentService.AddRowTable(rows, new int[] { 2500, 1250, 1250, 1250, 1250, 3852 });
            //    _wordDocumentService.AddTableHeader("C.1 CLINICIAN ACTION TAKEN WITH REGARD TO MEDICINE");
            //    _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
            //    _wordDocumentService.AddTableHeader("C.2 EFFECT OF DECHALLENGE/ RECHALLENGE");
            //    _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
            //    _wordDocumentService.AddTableHeader("C.3 NOTES");
            //    _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
            //}
            #endregion


        }

        #endregion





        private List<KeyValuePair<string, string>> PrepareNotes(string notes)
        {
            List<KeyValuePair<string, string>> rows = new();
            rows.Add(new KeyValuePair<string, string>("Notes", notes));
            return rows;
        }

        private List<KeyValuePair<string, string>> PrepareConditionsForActiveReport(PatientClinicalEvent patientClinicalEvent)
        {
            List<KeyValuePair<string, string>> rows = new();

            var i = 0;
            foreach (PatientCondition patientCondition in patientClinicalEvent.Patient.PatientConditions.Where(pc => pc.OnsetDate <= patientClinicalEvent.OnsetDate).OrderByDescending(c => c.OnsetDate))
            {
                i += 1;
                rows.Add(new KeyValuePair<string, string>("Condition", patientCondition.TerminologyMedDra.MedDraTerm));
                rows.Add(new KeyValuePair<string, string>("Start Date (yyyy-mm-dd)", patientCondition.OnsetDate.ToString("yyyy-MM-dd")));
                rows.Add(new KeyValuePair<string, string>("End Date (yyyy-mm-dd)", patientCondition.OutcomeDate.HasValue ? patientCondition.OutcomeDate.Value.ToString("yyyy-MM-dd") : ""));
            }

            return rows;
        }

        private async Task<List<KeyValuePair<string, string>>> PrepareAdverseEventForActiveReportAsync(PatientClinicalEvent patientClinicalEvent, ReportInstance reportInstance, bool isSerious)
        {
            var extendable = (IExtendable)patientClinicalEvent;
            List<KeyValuePair<string, string>> rows = new();

            rows.Add(new KeyValuePair<string, string>("Source Description", patientClinicalEvent.SourceDescription));
            rows.Add(new KeyValuePair<string, string>("MedDRA Term", reportInstance.TerminologyMedDra?.MedDraTerm));
            rows.Add(new KeyValuePair<string, string>("Onset Date (yyyy-mm-dd)", patientClinicalEvent.OnsetDate.HasValue ? patientClinicalEvent.OnsetDate.Value.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Resolution Date (yyyy-mm-dd)", patientClinicalEvent.ResolutionDate.HasValue ? patientClinicalEvent.ResolutionDate.Value.ToString("yyyy-MM-dd") : ""));

            if (patientClinicalEvent.OnsetDate.HasValue && patientClinicalEvent.ResolutionDate.HasValue)
            {
                rows.Add(new KeyValuePair<string, string>("Duration", $"${(patientClinicalEvent.ResolutionDate.Value - patientClinicalEvent.OnsetDate.Value).Days} days"));
            }
            else
            {
                rows.Add(new KeyValuePair<string, string>("Duration", string.Empty));
            }

            rows.Add(new KeyValuePair<string, string>("Outcome", await _attributeService.GetCustomAttributeValueAsync("PatientClinicalEvent", "Outcome", extendable)));

            if (isSerious)
            {
                rows.Add(new KeyValuePair<string, string>("Seriousness", await _attributeService.GetCustomAttributeValueAsync("PatientClinicalEvent", "Seriousness", extendable)));
                rows.Add(new KeyValuePair<string, string>("Classification", ReportClassification.From(reportInstance.ReportClassificationId).Name));
                rows.Add(new KeyValuePair<string, string>("Severity Grade", await _attributeService.GetCustomAttributeValueAsync("PatientClinicalEvent", "Severity Grade", extendable)));
                rows.Add(new KeyValuePair<string, string>("SAE Number", await _attributeService.GetCustomAttributeValueAsync("PatientClinicalEvent", "FDA SAE Number", extendable)));
            }

            return rows;
        }

        private async Task<List<KeyValuePair<string, string>>> PrepareAdverseEventForSpontaneousReportAsync(DatasetInstance datasetInstance, bool isSerious)
        {
            List<KeyValuePair<string, string>> rows = new();

            var reportInstance = await _reportInstanceRepository.GetAsync(ri => ri.ContextGuid == datasetInstance.DatasetInstanceGuid, new string[] { "TerminologyMedDra" });

            DateTime.TryParse(datasetInstance.GetInstanceValue("Reaction known start date"), out var onsetDateValue);
            DateTime.TryParse(datasetInstance.GetInstanceValue("Reaction date of recovery"), out var resolutionDateValue);

            rows.Add(new KeyValuePair<string, string>("Source Description", datasetInstance.GetInstanceValue("Description of reaction")));
            rows.Add(new KeyValuePair<string, string>("MedDRA Term", reportInstance.TerminologyMedDra?.MedDraTerm));
            rows.Add(new KeyValuePair<string, string>("Onset Date", onsetDateValue > DateTime.MinValue ? onsetDateValue.ToString("yyyy-MM-dd") : ""));
            rows.Add(new KeyValuePair<string, string>("Resolution Date", resolutionDateValue > DateTime.MinValue ? resolutionDateValue.ToString("yyyy-MM-dd") : ""));

            if (onsetDateValue > DateTime.MinValue && resolutionDateValue > DateTime.MinValue)
            {
                rows.Add(new KeyValuePair<string, string>("Duration", $"${(resolutionDateValue - onsetDateValue).Days} days"));
            }
            else
            {
                rows.Add(new KeyValuePair<string, string>("Duration", string.Empty));
            }

            rows.Add(new KeyValuePair<string, string>("Outcome", datasetInstance.GetInstanceValue("Outcome of reaction")));

            if (isSerious)
            {
                rows.Add(new KeyValuePair<string, string>("Seriousness", datasetInstance.GetInstanceValue("Reaction serious details")));
                rows.Add(new KeyValuePair<string, string>("Grading Scale", string.Empty));
                rows.Add(new KeyValuePair<string, string>("Severity Grade", string.Empty));
                rows.Add(new KeyValuePair<string, string>("SAE Number", string.Empty));
            }

            return rows;
        }

        private async Task PrepareMedicationsForActiveReportAsync(ReportInstance reportInstance)
        {
            var i = 0;
            foreach (ReportInstanceMedication reportMedication in reportInstance.Medications)
            {
                var patientMedication = await _patientMedicationRepository.GetAsync(pm => pm.PatientMedicationGuid == reportMedication.ReportInstanceMedicationGuid);
                if (patientMedication != null)
                {
                    i += 1;

                    List<string[]> rows = new();
                    List<string> cells = new();

                    cells.Add("Drug");
                    cells.Add("Start Date (yyyy-mm-dd)");
                    cells.Add("End Date (yyyy-mm-dd)");
                    cells.Add("Dose");
                    cells.Add("Route");
                    cells.Add("Causality");

                    rows.Add(cells.ToArray());

                    cells = new();

                    IExtendable extendable = patientMedication;
                    cells.Add(patientMedication.DisplayName);
                    cells.Add(patientMedication.StartDate.ToString("yyyy-MM-dd"));
                    cells.Add(patientMedication.EndDate.HasValue ? patientMedication.EndDate.Value.ToString("yyyy-MM-dd") : "");
                    cells.Add($"{patientMedication.Dose} {patientMedication.DoseUnit}");
                    cells.Add(await _attributeService.GetCustomAttributeValueAsync("PatientMedication", "Route", extendable));
                    cells.Add($"{reportMedication.WhoCausality} {reportMedication.NaranjoCausality}");

                    rows.Add(cells.ToArray());

                    _wordDocumentService.AddRowTable(rows, new int[] { 2500, 1250, 1250, 1250, 1250, 3852 });
                    _wordDocumentService.AddTableHeader("D.1 CLINICIAN ACTION TAKEN WITH REGARD TO MEDICINE");
                    _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
                    _wordDocumentService.AddTableHeader("D.2 EFFECT OF DECHALLENGE/RECHALLENGE");
                    _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
                    _wordDocumentService.AddTableHeader("D.3 NOTES");
                    _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
                }
            }
        }

        private async Task PrepareMedicationsForSpontaneousReportAsync(DatasetInstance datasetInstance)
        {
            var reportInstance = await _reportInstanceRepository.GetAsync(ri => ri.ContextGuid == datasetInstance.DatasetInstanceGuid, new string[] { "Medications" });
            if (reportInstance == null) { return; };

            var sourceProductElement = await _datasetElementRepository.GetAsync(dse => dse.ElementName == "Product Information");
            var sourceContexts = datasetInstance.GetInstanceSubValuesContext(sourceProductElement.ElementName);

            var i = 0;
            foreach (ReportInstanceMedication med in reportInstance.Medications)
            {
                i += 1;

                List<string[]> rows = new();
                List<string> cells = new();

                cells.Add($"Drug {i}");
                cells.Add("Start Date");
                cells.Add("End Date");
                cells.Add("Dose");
                cells.Add("Route");
                cells.Add("Indication");

                rows.Add(cells.ToArray());

                cells = new();

                var drugItemValues = datasetInstance.GetInstanceSubValues(sourceProductElement.ElementName, med.ReportInstanceMedicationGuid);

                var startValue = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug Start Date");
                var endValue = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug End Date");
                var dose = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Dose number");
                var route = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug route of administration");
                var indication = drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Drug Indication");

                cells.Add(drugItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Product").InstanceValue);
                cells.Add(startValue != null ? Convert.ToDateTime(startValue.InstanceValue).ToString("yyyy-MM-dd") : string.Empty);
                cells.Add(endValue != null ? Convert.ToDateTime(endValue.InstanceValue).ToString("yyyy-MM-dd") : string.Empty);
                cells.Add(dose != null ? dose.InstanceValue : string.Empty);
                cells.Add(route != null ? route.InstanceValue : string.Empty);
                cells.Add(indication != null ? indication.InstanceValue : string.Empty);

                rows.Add(cells.ToArray());

                _wordDocumentService.AddRowTable(rows, new int[] { 2500, 1250, 1250, 1250, 1250, 3852 });
                _wordDocumentService.AddTableHeader("C.1 CLINICIAN ACTION TAKEN WITH REGARD TO MEDICINE");
                _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
                _wordDocumentService.AddTableHeader("C.2 EFFECT OF DECHALLENGE/ RECHALLENGE");
                _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
                _wordDocumentService.AddTableHeader("C.3 NOTES");
                _wordDocumentService.AddOneColumnTable(new List<string>() { "" });
            }
        }

        private void PrepareEvaluationsForActiveReport(PatientClinicalEvent patientClinicalEvent)
        {
            List<string[]> rows = new();
            List<string> cells = new();

            cells.Add("Test");
            cells.Add("Test Date (yyyy-mm-dd)");
            cells.Add("Test Result");

            rows.Add(cells.ToArray());

            foreach (PatientLabTest labTest in patientClinicalEvent.Patient.PatientLabTests.Where(lt => lt.TestDate >= patientClinicalEvent.OnsetDate).OrderByDescending(lt => lt.TestDate))
            {
                cells = new();

                cells.Add(labTest.LabTest.Description);
                cells.Add(labTest.TestDate.ToString("yyyy-MM-dd"));
                cells.Add(labTest.TestResult);

                rows.Add(cells.ToArray());
            }

            _wordDocumentService.AddRowTable(rows, new int[] { 2500, 2500, 6352 });
        }

        private async Task PrepareEvaluationsForSpontaneousReport(DatasetInstance datasetInstance)
        {
            List<string[]> rows = new();
            List<string> cells = new();

            cells.Add("Test");
            cells.Add("Test Date");
            cells.Add("Test Result");

            rows.Add(cells.ToArray());

            var sourceLabElement = await _datasetElementRepository.GetAsync(dse => dse.ElementName == "Test Results");
            var sourceContexts = datasetInstance.GetInstanceSubValuesContext(sourceLabElement.ElementName);

            foreach (Guid sourceContext in sourceContexts)
            {
                var labItemValues = datasetInstance.GetInstanceSubValues(sourceLabElement.ElementName, sourceContext);

                var testDate = labItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Test Date");
                var testResult = labItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Test Result");

                cells = new();

                cells.Add(labItemValues.SingleOrDefault(div => div.DatasetElementSub.ElementName == "Test Name").InstanceValue);
                cells.Add(testDate != null ? Convert.ToDateTime(testDate.InstanceValue).ToString("yyyy-MM-dd") : string.Empty);
                cells.Add(testResult != null ? testResult.InstanceValue : string.Empty);

                rows.Add(cells.ToArray());
            }

            _wordDocumentService.AddRowTable(rows, new int[] { 2500, 2500, 6352 });
        }

        private void PrepareWeightForActiveReport(PatientClinicalEvent patientClinicalEvent)
        {
            List<string[]> rows = new();
            List<string> cells = new();

            cells.Add("Weight Date");
            cells.Add("Weight");

            rows.Add(cells.ToArray());

            var weightSeries = _patientService.GetElementValues(patientClinicalEvent.Patient.Id, "Weight(kg)", 10);

            if (weightSeries.Length > 0)
            {
                foreach (var weight in weightSeries[0].Series)
                {
                    cells = new();

                    cells.Add(weight.Name);
                    cells.Add(weight.Value.ToString());

                    rows.Add(cells.ToArray());
                }
            }

            _wordDocumentService.AddRowTable(rows, new int[] { 2500, 8852 });
        }

        private int CalculateAge(DateTime birthDate, DateTime onsetDate)
        {
            var age = onsetDate.Year - birthDate.Year;
            if (onsetDate > birthDate.AddYears(-age)) age--;
            return age;
        }
    }
}