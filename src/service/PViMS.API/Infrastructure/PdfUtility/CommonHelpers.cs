using OpenXmlPowerTools;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace PVIMS.API.Infrastructure.PdfUtility
{
    public class CommonHelpers
    {
        public static string ConvertDataTableToHTML(DataTable dt, bool is_header_required = false, int table_width_percentage = 100, int font_size = 12, string font_family = "helvetica")
        {
            //string html = "<table style='border:1px solid #b3adad;border-collapse:collapse;padding:5px;width:" + table_width_percentage + "%; font-family:" + font_family + ";font-size:" + font_size + "px'>";
            string html = $"<table style='border:1px solid #b3adad;border-collapse:collapse;padding:5px;width:{table_width_percentage}%;font-size:{font_size}px'>";

            //add header row
            if (is_header_required == true)
            {
                html += "<tr>";
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += "<td style='border:1px solid #b3adad;text-align:center;padding:5px;background-color: antiquewhite;color: #313030;'>" + dt.Columns[i].ColumnName + "</td>";
                html += "</tr>";
            }
            //add rows
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                html += "<tr>";
                for (int j = 0; j < dt.Columns.Count; j++)
                    html += "<td style='border:1px solid #b3adad;text-align:center;padding:5px;background-color: antiquewhite;color: #313030;'>" + dt.Rows[i][j].ToString() + "</td>";
                html += "</tr>";
            }
            html += "</table>";
            return html;
        }
        public static string ConvertDataTableToHTMLWithCustomClass(DataTable dt, bool is_header_required = false, int table_width_percentage = 100, int font_size = 12, string font_family = "helvetica", string custom_class = "customTable")
        {
            //string html = "<table style='border:1px solid #b3adad;border-collapse:collapse;padding:5px;width:" + table_width_percentage + "%; font-family:" + font_family + ";font-size:" + font_size + "px'>";
            string html = $"<table class='{custom_class}' style='border:1px solid #b3adad;border-collapse:collapse;padding:5px;width:{table_width_percentage}%;font-size:{font_size}px'>";

            //add header row
            if (is_header_required == true)
            {
                html += "<tr>";
                for (int i = 0; i < dt.Columns.Count; i++)
                    html += "<td style='border:1px solid #b3adad;text-align:center;padding:5px;background-color: #efef95;color: #313030;'>" + dt.Columns[i].ColumnName + "</td>";
                html += "</tr>";
            }
            //add rows
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                html += "<tr>";
                for (int j = 0; j < dt.Columns.Count; j++)
                    html += "<td style='border:1px solid #b3adad;text-align:center;padding:5px;background-color: #efef95;color: #313030;'>" + dt.Rows[i][j].ToString() + "</td>";
                html += "</tr>";
            }
            html += "</table>";
            return html;
        }
        public static string GeneratePDFFromHTMLWithCustomFont(string fileName, string fileSavePath, StringBuilder templatesBody, string web_root_path, string font_path)
        {
            string result = string.Empty;
            using (FileStream pdfDest = File.Open(Path.Combine(fileSavePath, fileName), FileMode.Create))
            {
               
                result = Path.Combine(fileSavePath, fileName);
            }
            return result;
        }
        public static DataTable ToDataTable(List<Dictionary<string, string>> list)
        {
            DataTable result = new();
            if (list.Count == 0)
                return result;

            var columnNames = list.SelectMany(dict => dict.Keys).Distinct();
            result.Columns.AddRange(columnNames.Select(c => new DataColumn(c)).ToArray());
            foreach (Dictionary<string, string> item in list)
            {
                var row = result.NewRow();
                foreach (var key in item.Keys)
                {
                    row[key] = item[key];
                }

                result.Rows.Add(row);
            }

            return result;
        }
    }
}
