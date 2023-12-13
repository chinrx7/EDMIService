using Newtonsoft.Json;
using ReportApi;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EDMIService
{

    internal class WRITE
    {
        public class Records
        {
            public string Timestamp { get; set; }
            public string Value { get; set; }
            public string Quality { get; set; }

        }

        public class Items
        {
            public string ItemName { get; set; }
            public List<Records> Records { get; set; }
        }
        public class DataSetss
        {
            public List<Items> DataSets { get; set; }
        }

        public static void ReceiveData2(DataSet datas)
        {
            DataTable dt = new DataTable();
            dt = datas.Tables[0];

            string ItemFormat = "";
            DateTime d = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            d.AddMonths(-1);

            foreach (DataRow dr in dt.Rows)
            {
                List<Records> records = new List<Records>();
                var rec = new Records();
                rec.Timestamp = d.ToString("yyyy-MM-dd");
                rec.Value = dr["value"].ToString();
                rec.Quality = "Good";
                records.Add(rec);

                string col = dr["Param"].ToString();

                if (col == "WH" || col == "VARH" || col == "W" )
                {
                    if (dr["Direction"].ToString() == "DELIVERED")
                    {
                        ItemFormat = "PLC1\\M2\\{0}\\TOU_{1}D";
                    }
                    else
                    {
                        ItemFormat = "PLC1\\M2\\{0}\\TOU_{1}R";
                    }

                    if (dr["Policy"].ToString() == "MAX")
                    {
                        ItemFormat = "PLC1\\M2\\{0}\\TOU_{1}MAX";
                    }
                }

                string WParam = string.Format(ItemFormat, dr["ChmiMap"].ToString(), dr["Param"].ToString());
                if (dr["TouRate"].ToString() == "TOTAL")
                {
                    WriteToAPI(records, WParam);
                }
            }
        }
        public static void ReceiveData(DataSet datas)
        {
            DataTable dt = new DataTable();
            dt = datas.Tables[0];

            DataView dv = new DataView(dt);
            DataTable disT = dv.ToTable(true, "Param","Direction","Phase");

            foreach(DataRow dr in disT.Rows)
            {
                var tt = dr[0];
                DataRow[] drRes = datas.Tables[0].Select("Param='" + dr[0] + "' AND Direction='" + dr[1] + "'");
                List<Records> records = new List<Records>();
                foreach(DataRow row in drRes)
                {
                    //Console.WriteLine(row[0] + " " + row[1] + " " + row[2]);
                    var Recrow = new Records();
                    Recrow.Timestamp = row[0].ToString();
                    Recrow.Value = row[1].ToString();
                    Recrow.Quality = "Good";

                    records.Add(Recrow);
                }
                var MAP = datas.Tables[0].Rows[0]["ChmiMap"];
                string ItemFormat = "";
                string drParam = dr[0].ToString();

                string pDir = dr["Direction"].ToString();

                if (drParam == "WH" || drParam == "VARH")
                {
                    if (pDir == "DELIVERED")
                    {
                        ItemFormat = "PLC1\\M2\\{0}\\LP_{1}D";
                    }
                    else if(pDir == "RECEIVED")
                    {
                        ItemFormat = "PLC1\\M2\\{0}\\LP_{1}R";
                    }
                }
                else if(drParam == "PF")
                {
                    ItemFormat = "PLC1\\M2\\{0}\\LP_{1}";
                }
                else
                {
                   ItemFormat = "PLC1\\M2\\{0}\\LP_{1}" + dr["Phase"].ToString();
                }

                //string WParam = "PLC1\\M2\\M201\\LP_" + dr[0] + "D";
                string WParam = string.Format(ItemFormat, MAP, dr[0]);
                WriteToAPI(records,WParam);
                
            }
        }

        public static void WriteToAPI(List<Records> Datas, string ItemName)
        {
            var jsonObj = new DataSetss();
            jsonObj.DataSets = new List<Items>
            {
                //new Items { ItemName = "PLC1\\M2\\M201\\LP_VARHD", Records = records }
                new Items { ItemName = ItemName , Records=Datas},
            };

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://" + Program._settings.CHMI_HOST + "/api/data/write");
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json";

            using(var streamWriter = new System.IO.StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = JsonConvert.SerializeObject(jsonObj);
                //Console.WriteLine(json);
                streamWriter.Write(json);
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using(var streamReader = new System.IO.StreamReader(httpResponse.GetResponseStream()))
            {
                var res = streamReader.ReadToEnd();
                //Console.WriteLine("Res: " + res);
            }

            //Util.Logging("Write To API", "Success");
         }

        }

    }
