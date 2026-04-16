using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BizUtil
{
    public class CaptureService
    {
        private readonly string _serviceUrl;

        public CaptureService(string serviceUrl)
        {
            _serviceUrl = serviceUrl;
        }
        public List<PackageCaptureData> ExtractCaptureData(string source)
        {
            string msgstring = "";
            //List<PackageCaptureData> OutData;
            int NetWeight;
            string MCode;

            try
            {
                var list = JsonSerializer.Deserialize<List<CaptureData>>(source);
                if (list != null)
                {
                    List<PackageCaptureData> OutData = new List<PackageCaptureData>();
                    foreach (CaptureData? CaptureData in list)
                    {

                        msgstring += ($"Name: {CaptureData?.articleName}\n");
                        msgstring += ($"Number: {CaptureData?.articleNumber}\n");
                        msgstring += ($"Weight: {Convert.ToInt32(CaptureData?.actualNetWeight?.value * 1000)}\n");
                        msgstring += ($"ErrFlag: {CaptureData?.error.flag}\n");
                        
                        

                        NetWeight = Convert.ToInt32(CaptureData?.actualNetWeight?.value * 1000);

                        if (CaptureData?.customFields != null)
                        {
                            foreach (customFields cf in CaptureData.customFields)
                            {
                                //msgstring += ($"CustFieldName: {cf.name}\n");
                                if (cf.name == "BARCODE")
                                {
                                    MCode = cf.value;
                                    OutData.Add(new PackageCaptureData(NetWeight, MCode));
                                                                    }
                                if (cf.name == "GTIN")
                                {
                                    
                                    msgstring += ($"GTIN: {cf.value}\n");
                                }
                            }

                        }
                    }
                    Program.ToLog($"ExtractCaptureData: считано записей {OutData.Count()}");
                    Program.ToLog(msgstring);
                    return OutData;
                }
                return null;
            }
            catch (Exception ex)
            { 
                throw new Exception(ex.ToString());
            }
        }
        public async Task<string> GetCaptureData()
        {
            string outString = "";
            string bearerToken = "";
            var postData = new
            {
                userName = "bizuser",       // Администратор
                password = "bizerba"        // 12345678
            };
            HttpClient client = new HttpClient();
            ///client.BaseAddress = new Uri( _serviceUrl);   /// ++++++++++++++++++
            // получение токена
            var json = System.Text.Json.JsonSerializer.Serialize(postData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"http://{Program.brain2Ip}:9997/api/v1/token";
            ///var url = "token";  ///++++++++++++++++++++++


            HttpResponseMessage response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                json = await response.Content.ReadAsStringAsync();
                BizResponse bizResponse = JsonSerializer.Deserialize<BizResponse>(json);
                bearerToken = bizResponse.token;
            }
            else
            {
                throw new Exception("GetCaptureData(): ошибка при получении токена!");
            }
            // получение данных из capture
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            using HttpResponseMessage response2 = await client.GetAsync($"http://{Program.brain2Ip}:9997/api/v1/package-records?packageType=singlePackage&take=20&sort=timestamp-");
            ///++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            //using HttpResponseMessage response2 = await client.GetAsync("package-records?packageType=singlePackage&take=20&sort=timestamp-");

            if (response2.IsSuccessStatusCode)
            {
                outString = await response2.Content.ReadAsStringAsync();
                Program.ToLog("GetCaptureData()");
            }
            else
            {
                string errString =  $"GetCaptureData(): ошибка при получении данных capture ({response2.StatusCode})!";
                throw new Exception(errString);
            }
            return outString;
        }
    }

    //----------------------------------------------------
    public class CaptureData
    {
        //   public DateTimeOffset Date { get; set; }
        // public int TemperatureCelsius { get; set; }
        public string? articleName { get; set; }
        public string? articleNumber { get; set; }
        public actualNetWeight? actualNetWeight { get; set; }
        public IList<customFields>? customFields { get; set; }
        public error error { get; set; }
    }
    public class error
    {
        public string? none { get; set; }
        public int flag { get; set; }
    }
    public class actualNetWeight
    {
        public decimal? value { get; set; }
        public int decimalPlaces { get; set; }
        public string? unit { get; set; }
    }

    public class customFields
    {
        public string? name { get; set; }
        public string? displayName { get; set; }
        public string? value { get; set; }
        public string? type { get; set; }
        public string? gtin { get; set; }
    }
    //----------------------------------------------------
    public class BizResponse
    {
        public User user { get; set; }
        public string? token { get; set; }
    }
    public class User
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? language { get; set; }
    }
    //----------------------------------------------------

    public class PackageCaptureData
    {

        public PackageCaptureData(int _netweight, string _barcode)
        {
            this.Weight = _netweight;
            this.Code = _barcode;
        }
        public int Weight { get; set; }

        public string? Code { get; set; }

    }
    public class CaptureRows : IEnumerable
    {
        private PackageCaptureData[] _crows;
        public CaptureRows(PackageCaptureData[] pArray)
        {
            _crows = new PackageCaptureData[pArray.Length];

            for (int i = 0; i < pArray.Length; i++)
            {
                _crows[i] = pArray[i];
            }
        }

        // Implementation for the GetEnumerator method.
        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }

        public CaptureEnum GetEnumerator()
        {
            return new CaptureEnum(_crows);
        }
        // When you implement IEnumerable, you must also implement IEnumerator.
        public class CaptureEnum : IEnumerator
        {
            public PackageCaptureData[] _crows;

            // Enumerators are positioned before the first element
            // until the first MoveNext() call.
            int position = -1;

            public CaptureEnum(PackageCaptureData[] list)
            {
                _crows = list;
            }

            public bool MoveNext()
            {
                position++;
                return (position < _crows.Length);
            }

            public void Reset()
            {
                position = -1;
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public PackageCaptureData Current
            {
                get
                {
                    try
                    {
                        return _crows[position];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }
    }
}

