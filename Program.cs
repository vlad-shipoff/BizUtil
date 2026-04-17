using BizUtil;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using static System.Net.WebRequestMethods;


namespace BizUtil
{
    internal class Program
    {
        static string bcsConnectionString;
        static string dmConnectionString;
        static string ftpServer;
        static string ftpPort;
        static string dmWorkFolder;
        static string bcsQuery;
        static string dmQuery;
        static string orderNumber;
        static string gtin;
        public static string brain2Ip;
        static string glpName;
        static string license;
        static bool autoClear;

        static async Task Main(string[] args)
        {
            string result = "";
            List<string> codes;
            List<PackageCaptureData> weights = new List<PackageCaptureData>();

            if (args.Length == 0)
            {
                Console.WriteLine("Bizutil -put | -get\n-put - запись кодов в этикетировщик\n-get - получить .json с данными веса");
                return;
            }
            try
            {
                ToLog("Старт программы");
                ToLog("GetConfiguration");
                GetConfiguration();
                ToLog("GetOrderNumber");
                GetOrderNumber();

                if (args[0] == "-put")  // отправка кодов
                {
                    ToLog("GetCodes");
                    codes = GetCodes();
                    if (codes != null)
                    {
                        if (codes.Count > 0)
                        {
                            ToLog("GenerateFileContent");
                            string fileContext = GenerateFileContent(codes);
                            if (autoClear) ClearBuffer();  // _connect.BRAIN Professional ?
                            ToLog("UploadFile");
                            UploadFile(fileContext);
                        }
                    }
                }
                else        // генерация .json
                {
                    if (license == "Capture")  // 
                    {
                        //----------------------------------------------------------
                        string fileName = "CaptureData2.json";

                        using (FileStream fileStream = System.IO.File.OpenRead(fileName))
                        {
                            Encoding encoding = Encoding.UTF8;

                            using (StreamReader streamReader = new StreamReader(fileStream, encoding))
                            result = streamReader.ReadToEnd();  // Читать поток и конвертировать его в строку
                            string wstr = "u001D";
                            result = result.Replace("@1D", $"\\{wstr}");
                        }
                        //----------------------------------------------------------
                        CaptureService captureService = new CaptureService($"http://{Program.brain2Ip}:9997/api/v1/");
                        ToLog("GetCaptureData");
                        //result = captureService.GetCaptureData().ToString();
                        if (result.Length > 50)
                            weights = captureService.ExtractCaptureData(result);
                        else ToLog("GetCaptureData: ошибка получения данных!");
                    }
                    else            // 2DB
                    {
                        ToLog("GetWeightData");
                        weights = GetWeightData();
                    }
                    if(weights.Count > 0)
                    {
                        SaveJson(weights);
                        ToLog($"SaveJson: сохранено записей {weights.Count()}");

                    }
                }
            }
            catch (Exception ex)
            {
                ToLog(ex.ToString());
                Console.WriteLine(ex.ToString());
            }
        }
        static void GetConfiguration()
        {
            try
            {
                bcsConnectionString = ConfigurationManager.ConnectionStrings["Bct2DbConnection"].ConnectionString;
                dmConnectionString = ConfigurationManager.ConnectionStrings["DmDbConnection"].ConnectionString;
                ftpServer = ConfigurationManager.AppSettings["FtpServer"].ToString();
                ftpPort = ConfigurationManager.AppSettings["FtpPort"].ToString();
                dmQuery = ConfigurationManager.AppSettings["DmQuery"].ToString();
                dmWorkFolder = ConfigurationManager.AppSettings["DmWorkFolder"].ToString();
                bcsQuery = ConfigurationManager.AppSettings["BcsQuery"].ToString();
                //gtin = ConfigurationManager.AppSettings["CurrentGTIN"].ToString().Substring(1, 12);   // 
                brain2Ip = ConfigurationManager.AppSettings["Brain2Ip"].ToString();   // 
                glpName = ConfigurationManager.AppSettings["GlpName"].ToString();   // 
                autoClear = Boolean.Parse(ConfigurationManager.AppSettings["AutoClear"].ToString());   // _connect.Brain Professional ?
                license = ConfigurationManager.AppSettings["License"].ToString();
            }
            catch (Exception ex)
            {
                ToLog(ex.ToString());
                throw new Exception("Ошибка чтения конфигурации!");
            }
        }
        static void GetOrderNumber()
        {
            string[] lines;
            string fullPath;

            try
            {
                fullPath = dmWorkFolder + "\\self.txt";
                lines = System.IO.File.ReadAllLines(fullPath);
                if (lines != null)
                {
                    orderNumber = lines[0];
                    if (String.IsNullOrEmpty(orderNumber))
                    {
                        throw new Exception($"Файл {fullPath} не содержит номер заказа!");
                    }
                    gtin = lines[5];
                    if (String.IsNullOrEmpty(gtin))
                    {
                        throw new Exception($"Файл {fullPath} не содержит GTIN!");
                    }
                    if(gtin.Length==14)
                        gtin=gtin.Substring(0,13);
                    ////-----------------------------------------------------
                    //Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    //configuration.AppSettings.Settings["CurrentGTIN"].Value = gtin;   // сохранение текущего GTIN
                    //ConfigurationSectionCollection sections = configuration.Sections;
                    ////-----------------------------------------------------
                    ToLog($"Заказ: {orderNumber}, GTIN: {gtin}");
                }
            }
            catch (Exception ex)
            {
                ToLog(ex.ToString());
                throw new Exception($"Ошибка чтения {dmWorkFolder}\\self.txt\n{ex.ToString()}");
            }
        }
        static List<string> GetCodes()
        {
            List<string> codes = new List<string>();
            SqlConnection sqlCon; // = new SqlConnection(dmConnectionString);
            SqlCommand sqlCmd; // = new SqlCommand(dmQuery + orderNumber);
            int cnt = 0;
            try
            {
                using (sqlCon = new SqlConnection(dmConnectionString))
                {
                    sqlCon.Open();
                    sqlCmd = new SqlCommand(dmQuery, sqlCon);
                    sqlCmd.Parameters.AddWithValue("@ID_ORDER", orderNumber);
                    SqlDataReader rdr = sqlCmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        codes.Add(rdr["kod_KM"].ToString());
                        cnt++;
                    }
                    sqlCon.Close();
                    ToLog($"Прочитано записей: {cnt}");
                }
                return codes;
            }
            catch (Exception ex)
            {
                ToLog(ex.ToString());
                throw new Exception(ex.ToString());
            }
        }
        static string GenerateFileContent(List<string> codes)
        {
            StringBuilder sb = new StringBuilder();
            string s21, s91, s92, s93;
            int pos, i;
            bool longCod;

            if (codes[0].Length > 50)           // длинный криптохвост
            {
                longCod = true;
                sb.AppendLine("GL6E|GT61|GT62|GT63|");
            }
            else                                // короткий криптохвост    
            {
                longCod = false;
                sb.AppendLine("GL6E|GT61|GT62|");
            }

            try
            {
                for (i = 0; i < codes.Count; i++)
                {
                    if (longCod)   // длинный криптохвост
                    {
                        s21 = codes[i].Substring(18, 13);
                        s91 = codes[i].Substring(34, 4);
                        pos = codes[i].IndexOf("\u001d92");
                        s92 = codes[i].Substring(pos + 2);

                        sb.AppendLine($"{i + 1}|{s21}|{s91}|{s92}|");
                    }
                    else           // короткий криптохвост
                    {
                        s21 = codes[i].Substring(18, 6);
                        //pos = kMs[i].kod_KM.IndexOf("\u001d93");
                        s93 = codes[i].Substring(27);

                        sb.AppendLine($"{i + 1}|{s21}|{s93}|");
                    }
                }
                ToLog($"Записано строк: {i}");
            }
            catch (Exception e)
            {
                ToLog(e.ToString());
                throw new Exception(e.ToString());
            }
            return sb.ToString();
        }
        static void UploadFile(string content)
        {
            string username = "bizuser";
            string password = "bizerba";
            string ftpUri = $"ftp://{ftpServer}/uniquePckData/codes.txt";

            Ping ping = new Ping();
            try
            {
                PingReply pingreply = ping.Send(ftpServer, 2000);
                if (pingreply.Status != IPStatus.Success)
                {
                    throw new Exception($"Отсутствует ping с устройством {ftpServer}!");
                }
            }
            catch (PingException ex)
            {
                ToLog(ex.ToString());
                throw new Exception(ex.ToString());

            }
            try
            {
                string tempFilePath = Path.GetTempFileName();
                tempFilePath = tempFilePath.Replace(".tmp", ".txt");
                System.IO.File.WriteAllText(tempFilePath, content, Encoding.ASCII);

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUri);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(username, password);
                request.UseBinary = true;
                // request.UsePassive = true; // Настройте в зависимости от требований FTP-сервера

                using (Stream fileStream = System.IO.File.OpenRead(tempFilePath))
                using (Stream ftpStream = request.GetRequestStream())
                {
                    fileStream.CopyTo(ftpStream);
                }
            }
            catch (Exception ex)
            {
                ToLog(ex.ToString());
                throw new Exception("Ошибка записи на FTP сервер!\n" + ex.ToString());
            }
        }

        static List<PackageCaptureData> GetWeightData()
        {
            string s31, s21, s91, s92, s93, weight, code;
            List<PackageCaptureData> weightData = new List<PackageCaptureData>();
            SqlConnection sqlCon; // = new SqlConnection(dmConnectionString);
            SqlCommand sqlCmd; // = new SqlCommand(dmQuery + orderNumber);
            int i;
            try
            {
                using (sqlCon = new SqlConnection(bcsConnectionString))
                {
                    sqlCon.Open();
                    sqlCmd = new SqlCommand(bcsQuery, sqlCon);
                    sqlCmd.Parameters.AddWithValue("@GTIN", gtin);
                    SqlDataReader rdr = sqlCmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        s31 = rdr[4].ToString();
                        if (s31.Length == 12)
                            s31 = "0" + s31;
                        i = GetCheckDigit(s31);
                        s31 += i.ToString();
                        weight = rdr[3].ToString().Replace("KG;-3;", "");
                        if (rdr[7].ToString().Length > 10)  // длинный
                        {
                            s21 = rdr[5].ToString().Substring(0, 13);
                            s91 = rdr[6].ToString().Substring(0, 4);
                            s92 = rdr[7].ToString().Substring(0, 44);
                            code = "01" + s31.Substring(0, 14) + "21" + s21 + "\u001d91" + s91 + "\u001D92" + s92;
                        }
                        else                                          // короткий
                        {
                            s21 = rdr[5].ToString().Substring(0, 6).Replace("'", "''");
                            s93 = rdr[6].ToString().Substring(0, 4).Replace("'", "''");
                            code = "01" + s31 + "21" + s21 + "\u001d93" + s93;
                        }
                        //----------------------------------------------------
                        var wd = new PackageCaptureData (int.Parse( weight), code);
                        weightData.Add(wd);
                        //----------------------------------------------------
                    }
                    sqlCon.Close();
                    ToLog($"Прочитано записей: { weightData.Count()}");
                }
                return weightData;
            }
            catch (Exception ex)
            {
                ToLog(ex.ToString());
                throw new Exception(ex.ToString());
            }
        }
        static void SaveJson(List<PackageCaptureData> weights)
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(weights);
                string workFolder = Directory.GetCurrentDirectory();
                //string docPath = dmWorkDir;

                // Write the specified text asynchronously to a new file named "WriteTextAsync.txt".

                using (StreamWriter outputFile = new StreamWriter(Path.Combine(workFolder, "JSON\\" + orderNumber + ".json")))
                {
                    outputFile.Write(jsonString);
                }
            }
            catch (Exception ex)
            {
                ToLog(ex.ToString());
                throw new Exception(ex.ToString());
            }
        }
        public static void ToLog(string str)
        {
            System.IO.File.AppendAllText("BizUtil.log", DateTime.Now.ToString("\ndd-MM-yy hh:mm:ss    ") + str);
        }

        public static int GetCheckDigit(string str)
        {
            int sum1 = 0, sum2 = 0, check = -1, pos = 1;

            if (str.Length != 13)
            {
                return (-1);
            }
            for (int i = str.Length - 1; i >= 0; i--, pos++)
            {
                if (pos % 2 == 0)
                {
                    sum2 += (int)Char.GetNumericValue(str[i]);
                }
                else
                {
                    sum1 += (int)Char.GetNumericValue(str[i]);
                }
            }
            int ii = (sum1 * 3 + sum2) % 10;
            if (ii != 0)
                check = 10 - ii;
            else check = 0;

            return (check);
        }
        static async void ClearBuffer()
        {
            HttpClient client = new HttpClient();

            // базовый адрес для запросов - выключить уникальные данные
            client.BaseAddress = new Uri($"http://{brain2Ip}:2020/ConnectService/json/SendMessage?connectName={glpName}&message=A!GW7D|0&timeout=200");
            using HttpResponseMessage response = await client.GetAsync("resource");
            if (response.IsSuccessStatusCode)              // Проверяем успешность ответа
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                ToLog("ClearBuffer Codes Off: "+responseBody);
            }
            else
            {
                string errString = $"ClearBuffer Codes Off Ошибка: {response.StatusCode} - {response.ReasonPhrase}";
                throw new Exception(errString);
            }
            // очистить уникальные данные
            client.BaseAddress = new Uri($"http://{brain2Ip}:2020/ConnectService/json/SendMessage?connectName={glpName}&message=A!XX13&timeout=200");
            using HttpResponseMessage response2 = await client.GetAsync("resource");

            if (response2.IsSuccessStatusCode)
            {
                string response2Body = await response2.Content.ReadAsStringAsync();
                ToLog("ClearBuffer Clear Codes: " + response2Body);
            }
            else
            {
                string errString = $"ClearBuffer Clear Codes Ошибка: {response2.StatusCode} - {response2.ReasonPhrase}";
                throw new Exception(errString);
            }

            // включить уникальные данные
            client.BaseAddress = new Uri($"http://{brain2Ip}:2020/ConnectService/json/SendMessage?connectName={glpName}&message=A!GW7D|1&timeout=200");
            using HttpResponseMessage response3 = await client.GetAsync("resource");

            if (response3.IsSuccessStatusCode)
            {
                string responseBody = await response3.Content.ReadAsStringAsync();
                ToLog("ClearBuffer Codes On: " + responseBody);
            }
            else
            {
                string errString = $"ClearBuffer Codes On Ошибка: {response3.StatusCode} - {response3.ReasonPhrase}";
                throw new Exception(errString);
            }
        }
        
    }
}


