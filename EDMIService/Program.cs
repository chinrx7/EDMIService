using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System;
using Microsoft.Extensions.Configuration;
using ReportApi;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ServiceProcess;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;
using System.Collections.Generic;

namespace EDMIService
{
    public class UserAuthen
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }


    internal class Program
    {
        public class Config
        {
            public string BaseUri { get; set; }
            public string LoadProfileUri { get; set; }
            public string LoadTOUri { get; set; }
            public string LoadDeviceUri { get; set; }
            public string AuthUri { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string CHMI_HOST { get; set; }
            public int ReadInterval { get; set; }
            public int Debug { get; set; }
        }

        public class Channels
        {
            public string units { get; set; }
            public string flowDirection { get; set; }
            public string apportionPolicy { get; set; }
            public string phase { get; set; }
        }

        public class Intervals
        {
            public string endOfInterval { get; set; }
            public string[] values { get; set; }
            public string[] statuses { get; set; }
        }

        public class registers
        {
            public string units { get; set; }
            public string flowDirection { get; set; }
            public string phase { get; set; }
            public string apportionPolicy { get; set; }
            public string touRate { get; set; }
            public registerValue registerValue { get; set; }

        }

        public class registerValue
        {
            public string value { get; set; }
        }

        public class BUILDING
        {
            public string PLANTID { get; set; }
            public string NAME { get; set; }
            public string CHMIMAP { get; set; }
            public string TYPE { get; set; }
        }


        public static string Utoken;
        public static Config _settings;
        public static IConfigurationRoot Configuration { get; set; }

        public static DataSet ds;
        public static DataSet ds2;

        public static System.Timers.Timer _Timer;

        public static string path;

        static void Main(string[] args)
        {
            Init();
            try
            {
                IHost host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices(services =>
                    {
                        services.AddHostedService<Worker>();

                    })
                    .UseWindowsService()
                    .Build();

                host.Run();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Application", ex.ToString(), EventLogEntryType.Error);
            }

        }



        static void StartTimer()
        {
            int interval = (_settings.ReadInterval * 60) * 1000;
            _Timer = new System.Timers.Timer();
            _Timer.Enabled = true;
            _Timer.AutoReset = true;
            _Timer.Interval = interval;
            _Timer.Elapsed += _Timer_Elapsed;
            _Timer.Start();
        }

        private static void _Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DoWork();
        }

        public static void DoWork()
        {
            GetToken();
            GetAllDevice();
            //WRITE.ReceiveData(ds);
            SetReadTime(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "read");
        }

        static void LoadConfig()
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            path = Path.GetDirectoryName(exe);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(path + "\\config.json", optional: false);

            Configuration = builder.Build();
            var _setting = Configuration.GetSection("Setting").Get<Config>();
            _settings = _setting;
            Util.Logging("LoadConfig", "Config Loaded");
        }

        static void GetToken()
        {
            using (var client = new HttpClient())
            {
                //Form Data
                var formData = "var1=val1&var2=val2";
                var encodedFormData = Encoding.ASCII.GetBytes(formData);

                String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(_settings.Username + ":" + _settings.Password));

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(_settings.BaseUri + _settings.AuthUri);
                httpWebRequest.ContentType = "application/x-www-form-urlencoded";
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentLength = encodedFormData.Length;
                httpWebRequest.Headers.Add("Authorization", "Basic " + encoded);
                httpWebRequest.PreAuthenticate = true;

                using (var stream = httpWebRequest.GetRequestStream())
                {
                    stream.Write(encodedFormData, 0, encodedFormData.Length);
                }

                HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

                dynamic json = JObject.Parse(responseString);
                Utoken = json["authToken"]["token"];
                Util.Logging("GetToken", "Got Token : " + Utoken);
            }
        }

        static string GetReadTime(string read)
        {
            string resDate = "";
            StreamReader r = new StreamReader(path + "\\" + read + ".json");

            var Js = r.ReadToEnd();
            dynamic json = JObject.Parse(Js);
            resDate = json["Read"]["ReadTime"];

            r.Close();
            return resDate;
        }

        static void SetReadTime(string Date, string read)
        {
            string json = File.ReadAllText(path + "\\" + read + ".json");
            dynamic js = JsonConvert.DeserializeObject(json);
            js["Read"]["ReadTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string op = JsonConvert.SerializeObject(js, Formatting.Indented);
            File.WriteAllText(read + ".json", op);
        }


        static void GetAllDevice()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            string jstring = File.ReadAllText(path + "\\building.json", Encoding.ASCII);

            dynamic json = JObject.Parse(jstring);

            JToken jtk = json["BUILDING"];

            var building = jtk.ToObject<BUILDING[]>();

            foreach (var bd in building)
            {
                if (checkTouDate())
                {
                    LoadTOU(bd.PLANTID, bd.CHMIMAP, bd.TYPE);
                }
                try
                {
                    loadProfile(bd.PLANTID, bd.CHMIMAP, bd.TYPE);
                }
                catch (Exception ex)
                {
                    Util.Logging("Load Profile", ex.Message);
                }
                //Console.WriteLine(bd.CHMIMAP);
            }
        }

        static async void loadDevice()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("Authorization", "AuthToken \"Token = " + Utoken + "\"");

                var res = client.GetAsync(_settings.BaseUri + _settings.LoadDeviceUri);

                if (res.Result.IsSuccessStatusCode)
                {
                    //var Con = res.Result.Content.ReadAsStringAsync();

                    var Con = await res.Result.Content.ReadAsByteArrayAsync();
                    var str = Encoding.ASCII.GetString(Con, 0, Con.Length - 1);
                    //Console.WriteLine(str);
                }
                //Console.WriteLine(res);
            }
        }


        public static void Init()
        {
            LoadConfig();
        }
        static async void loadProfile(string PlantID, string ChmiMap, string Type)
        {

            ds = new DataSet();
            ds.Tables.Add("loadprofile");
            ds.Tables[0].Columns.Add("TMP");
            ds.Tables[0].Columns.Add("Value");
            ds.Tables[0].Columns.Add("Param");
            ds.Tables[0].Columns.Add("Phase");
            ds.Tables[0].Columns.Add("PlantID");
            ds.Tables[0].Columns.Add("ChmiMap");
            ds.Tables[0].Columns.Add("Type");
            ds.Tables[0].Columns.Add("Direction");


            DateTime StartDate = DateTime.Parse(GetReadTime("read")).AddHours(-1);
            DateTime EndDate = StartDate.AddHours(1);

            DateTime CurTime = DateTime.Now;

            var Hour = (CurTime - StartDate).TotalHours;
            if (Hour > 1) { EndDate = CurTime; }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("Authorization", "AuthToken \"Token = " + Utoken + "\"");

                string UriForm = _settings.BaseUri + _settings.LoadProfileUri + "{0}?startDate={1}&endDate={2}";
                var ReqUri = string.Format(UriForm, PlantID, StartDate.ToString("MM/dd/yyyy HH:mm:ss"), EndDate.ToString("MM/dd/yyyy HH:mm:ss"));

                var res = client.GetAsync(ReqUri);


                if (res.Result.IsSuccessStatusCode)
                {
                    var Con = await res.Result.Content.ReadAsStringAsync();
                    dynamic json = JObject.Parse(Con);
                    //dynamic json = (JObject)JsonConvert.DeserializeObject(Con);
                    JToken jToken = null;
                    JToken jToken2 = null;

                    try
                    {
                        jToken = json["intervalDataSets"][0]["channels"];
                        jToken2 = json["intervalDataSets"][0]["intervals"];


                        var Chns = jToken.ToObject<Channels[]>();
                        var Ints = jToken2.ToObject<Intervals[]>();

                        string[] Params = new string[Chns.Length];
                        string[] Phases = new string[Chns.Length];
                        string[] Direction = new string[Chns.Length];

                        string path = "C:\\Export\\" + PlantID + ".CSV";
                        string header = "TimeStamp,";

                        for (int i = 0; i < Chns.Length; i++)
                        {
                            Params[i] = Chns[i].units;
                            Phases[i] = Chns[i].phase;
                            Direction[i] = Chns[i].flowDirection;
                            string col = Chns[i].units + "_" + Chns[i].phase + "_" + Chns[i].flowDirection + ",";
                            header += col;
                        }
                        header = header.Remove(header.Length - 1);

                        //header += Environment.NewLine;

                        StreamWriter sw = File.AppendText(path);
                        sw.WriteLine(header);

                        for (int i = 0; i < Ints.Length; i++)
                        {
                            string val = Ints[i].endOfInterval + ",";
                            for (int ii = 0; ii < Ints[i].values.Length; ii++)
                            {
                                val += Ints[i].values[ii] + ",";
                                DataRow dr = ds.Tables[0].NewRow();
                                dr["TMP"] = Ints[i].endOfInterval;
                                dr["Value"] = Ints[i].values[ii];
                                dr["Param"] = Params[ii];
                                dr["Phase"] = Phases[ii];
                                dr["PlantID"] = PlantID;
                                dr["ChmiMap"] = ChmiMap;
                                dr["Type"] = Type;
                                dr["Direction"] = Direction[ii];
                                ds.Tables[0].Rows.Add(dr);
                            }

                            val = val.Remove(val.Length - 1);
                            //val += Environment.NewLine;
                            sw.WriteLine(val);


                            //Console.WriteLine(Int.values[0] + " " + Int.values[1]);
                        }
                        sw.Close();
                        WRITE.ReceiveData(ds);
                    }
                    catch (Exception ex)
                    {
                        Util.Logging("Loadprofile", ex.Message);
                    }

                    Util.Logging("LoadProfile", PlantID + " Get response success");
                }
                else
                {
                    var err = await res.Result.Content.ReadAsStringAsync();
                    Util.Logging("LoadProfile", PlantID + " Get response unsuccess");
                }
            }
        }



        static async void LoadTOU(string PlantID, string ChmiMap, string Type)
        {
            ds2 = new DataSet();
            ds2.Tables.Add("loadTou");
            ds2.Tables[0].Columns.Add("TIMESTAMP");
            ds2.Tables[0].Columns.Add("Param");
            ds2.Tables[0].Columns.Add("Direction");
            ds2.Tables[0].Columns.Add("Phase");
            ds2.Tables[0].Columns.Add("value");
            ds2.Tables[0].Columns.Add("PlantID");
            ds2.Tables[0].Columns.Add("ChmiMap");
            ds2.Tables[0].Columns.Add("Type");
            ds2.Tables[0].Columns.Add("Policy");

            DateTime StartDate, EndDate;
            StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            EndDate = StartDate.AddMonths(1).AddDays(-1);

            using (var client = new HttpClient())
            {

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("Authorization", "AuthToken \"Token = " + Utoken + "\"");

                string UriForm = _settings.BaseUri + _settings.LoadTOUri + "{0}?startDate={1}&endDate={2}";
                var ReqUri = string.Format(UriForm, PlantID, StartDate.ToString("MM/dd/yyyy HH:mm:ss"), EndDate.ToString("MM/dd/yyyy HH:mm:ss"));

                var res = client.GetAsync(ReqUri);

                if (res.Result.IsSuccessStatusCode)
                {
                    var Con = await res.Result.Content.ReadAsStringAsync();
                    dynamic json = JObject.Parse(Con);
                    //dynamic json = (JObject)JsonConvert.DeserializeObject(Con);


                    JToken jToken = json["registerDataSets"][0]["registers"];
                    //var regis = jToken.ToObject<registers>();

                    var myobjList = JsonConvert.DeserializeObject<List<registers>>(jToken.ToString());
                    DateTime dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    dateTime.AddMonths(-1);
                    for (int i = 0; i < myobjList.Count; i++)
                    {
                        DataRow dr = ds2.Tables[0].NewRow();
                        dr["TIMESTAMP"] = dateTime.ToString("yyyy-MM-01 00:00:00");
                        dr["Param"] = myobjList[i].units;
                        dr["Direction"] = myobjList[i].flowDirection;
                        dr["Phase"] = myobjList[i].phase;
                        dr["value"] = myobjList[i].registerValue.value;
                        dr["PlantID"] = PlantID;
                        dr["ChmiMap"] = ChmiMap;
                        dr["Type"] = Type;
                        dr["Policy"] = myobjList[i].apportionPolicy;
                        ds2.Tables[0].Rows.Add(dr);
                        //Console.WriteLine(myobjList[i].units + " " + myobjList[i].flowDirection + " " + myobjList[i].registerValue.value);
                    }

                    WRITE.ReceiveData2(ds2);
                    SetReadTime(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "read2");
                    Util.Logging("LOAD TOU", "SUCCESS");
                }

            }
        }

        public static bool checkTouDate()
        {
            bool res = false;
            DateTime fTime = DateTime.Parse(GetReadTime("read2"));
            if (fTime.ToString("MMM") != DateTime.Now.ToString("MMM"))
            {
                res = true;
            }
            return res;
        }

    }
}