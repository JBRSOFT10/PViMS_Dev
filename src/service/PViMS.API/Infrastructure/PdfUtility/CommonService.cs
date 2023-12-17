using PVIMS.Core.Aggregates.DatasetAggregate;
using PVIMS.Core.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PVIMS.API.Infrastructure.PdfUtility
{
    public class CommonService
    {
        public static DataTable GenerateDynamicData(DatasetElement sourceProductElement)
        {
            List<Dictionary<string, string>> keyValuePairs = new List<Dictionary<string, string>>();
    
            foreach (var dataset_instance in sourceProductElement.DatasetInstanceValues)
            {
                List<string> list = new List<string>();
                foreach (var dataset_instance_sub in dataset_instance.DatasetInstanceSubValues)
                {

                    if (!list.Contains(dataset_instance_sub.ContextValue.ToString()))
                    {
                        list.Add(dataset_instance_sub.ContextValue.ToString());
                    }
                }
                foreach (var guiID in list)
                {
                    Dictionary<string, string> test_data_items = new();
                    var brandValue = dataset_instance.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Brand/Trade name" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var genericName = dataset_instance.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Generic name" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var indicationValue = dataset_instance.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Indication" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var dosageValue = dataset_instance.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Dosage form" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);
                    var strengthValue = dataset_instance.DatasetInstanceSubValues.Where(x => x.DatasetElementSub.FriendlyName == "Strength & Frequency" && x.ContextValue.ToString() == guiID.ToString()).Select(x => x.InstanceValue);

                    test_data_items.Add("Brand/Trade name", brandValue.FirstOrDefault() ?? string.Empty);
                    test_data_items.Add("Generic Name", genericName.FirstOrDefault() ?? string.Empty);
                    test_data_items.Add("Indication", indicationValue.FirstOrDefault() ?? string.Empty);
                    test_data_items.Add("Dosage Form", dosageValue.FirstOrDefault() ?? string.Empty);
                    test_data_items.Add("Strength & Frequency", strengthValue.FirstOrDefault() ?? string.Empty);
                    keyValuePairs.Add(test_data_items);
                }


            }



            //List<MedicineInformation> medicineInformations = new List<MedicineInformation>();
            //medicineInformations.Add(new MedicineInformation
            //{
            //    brand_or_trade_name = "aaa",
            //    dosage_form = "bbb",
            //    generic_name = "ccc",
            //    indication = "ddd",
            //    strength_and_frequency = "eee"
            //});
            //medicineInformations.Add(new MedicineInformation
            //{
            //    brand_or_trade_name = "sdfs",
            //    dosage_form = "bfsdfbb",
            //    generic_name = "dsf",
            //    indication = "sdf",
            //    strength_and_frequency = "eesfsde"
            //});
            //foreach (var item in medicineInformations)
            //{
            //    test_data_items.Add("Brand/Trade name", item.brand_or_trade_name ?? string.Empty);
            //    test_data_items.Add("Generic Name", item.generic_name ?? string.Empty);
            //    test_data_items.Add("Indication", item.indication ?? string.Empty);
            //    test_data_items.Add("Dosage Form", item.dosage_form ?? string.Empty);
            //    test_data_items.Add("Strength & Frequency", item.strength_and_frequency ?? string.Empty);
            //    keyValuePairs.Add(test_data_items);
            //}
            DataTable data = CommonHelpers.ToDataTable(keyValuePairs);
            return data;
        }



        public class MedicineInformation
        {
            public string? brand_or_trade_name { get; set; }
            public string? generic_name { get; set; }
            public string? indication { get; set; }
            public string? dosage_form { get; set; }
            public string? strength_and_frequency { get; set; }
        }
    }
}
