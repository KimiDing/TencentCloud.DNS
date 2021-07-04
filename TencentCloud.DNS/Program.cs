using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using TencentCloud.DNS.TencentCloud.DNS.DTO;

namespace TencentCloud.DNS
{
    class Program
    {
        //最新的dns解析设置
        private static string latestIP = "";

        private static string sId = "";
        private static string sKey = "";

        private static HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            sId = GetSettings("Secret:SecretId");
            sKey = GetSettings("Secret:SecretKey");
            var flag = GetSettings("Setting:Flag");

            if (string.IsNullOrEmpty(sId) || string.IsNullOrEmpty(sKey))
            {
                Console.WriteLine($"请配置SecretId及SecretKey");
                Console.ReadLine();

                return;
            }

            if (flag == "1")
                GetRecordList();

            if (flag == "2")
            {
                var refreshTime = GetSettings("Setting:RefreshTime");

                if(int.TryParse(refreshTime,out int time))
                {
                    var recordId = GetSettings("ModifyParam:RecordId");
                    var domain = GetSettings("ModifyParam:Domain");
                    var subDomain = GetSettings("ModifyParam:SubDomain");

                    if(string.IsNullOrEmpty(recordId) || string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(subDomain))
                    {
                        Console.WriteLine("请配置修改解析的必要参数");
                        Console.ReadLine();
                        return;
                    }

                    //启动定时器 刷新修改
                    Timer threadTimer = new Timer(callback: new TimerCallback(RecordModify), new { rId=recordId,d=domain,sd=subDomain }, 0, time);
                }
                else
                {
                    Console.WriteLine($"刷新间隔配置必须为数字");
                }
                    
            }

            if (flag == "3")
                SetRecordStatus();

            Console.ReadLine();
        }

        /// <summary>
        /// 获取解析记录列表
        /// </summary>
        private static void GetRecordList()
        {
            var domain = GetSettings("RecordParam:Domain");
            if (string.IsNullOrEmpty(domain))
            {
                Console.WriteLine("域名配置为空");
                return;
            }

            var url = $"GETcns.api.qcloud.com/v2/index.php?Action=RecordList&Nonce={new Random().Next(0, 100)}&Region=&SecretId={sId}&SignatureMethod=HmacSHA256&Timestamp={Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds)}&domain={domain}";

            var signature = System.Web.HttpUtility.UrlEncode(CreateToken(url, sKey));

            var tencentResultStr = client.GetStringAsync("https://" + url.Substring(3) + $"&Signature={signature}").Result;

            var jsonOpt = new JsonSerializerOptions();
            jsonOpt.Converters.Add(new DatetimeJsonConverter());

            var tencentResult = JsonSerializer.Deserialize<TencentResult<TencentDomainResult>>(tencentResultStr, jsonOpt);
            if (tencentResult.code == 0)
            {
                if(long.Parse(tencentResult.data.info.record_total) > 0)
                {
                    foreach (var record in tencentResult.data.records)
                    {
                        Console.WriteLine($"{record.name}:{record.id}");
                    }
                }
                else
                {
                    Console.WriteLine("没有域名记录");
                }
                
            }
            else
            {
                Console.WriteLine($"获取失败:{tencentResult.message}");
            }

        }

        /// <summary>
        /// 修改解析记录
        /// </summary>
        /// <param name="sender"></param>
        private static void RecordModify(object sender)
        {
            //调用搜狐接口获取本机公网ip
            if (!GetIpBySohu(out string ip))
                return;

            if (ip == latestIP)
            {
                Console.WriteLine($"{DateTime.Now}  已存在最新解析:{latestIP}");
                return;
            }

            var param = (dynamic)sender;
            var recordId = param.rId;
            var domain = param.d;
            var subDomain = param.sd;

            var url = $"GETcns.api.qcloud.com/v2/index.php?Action=RecordModify&Nonce={new Random().Next(0, 100)}&Region=&SecretId={sId}&SignatureMethod=HmacSHA256&Timestamp={Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds)}&domain={domain}&recordId={recordId}&recordLine=默认&recordType=A&subDomain={subDomain}&value={ip}";


            var signature = System.Web.HttpUtility.UrlEncode(CreateToken(url, sKey));

            var tencentResultStr = client.GetStringAsync("https://" + url.Substring(3) + $"&Signature={signature}").Result;


            var tencentResult = JsonSerializer.Deserialize<TencentResult<object>>(tencentResultStr);
            if(tencentResult.code == 0)
            {
                latestIP = ip;
                Console.WriteLine($"{DateTime.Now}  设置解析:{ip}成功！");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now}  设置失败:{tencentResult.message}");
            }
            
        }

        /// <summary>
        /// 设置解析记录状态
        /// </summary>
        private static void SetRecordStatus()
        {
            var recordId = GetSettings("StatusParam:RecordId");
            var domain = GetSettings("StatusParam:Domain");
            var status = GetSettings("StatusParam:Status");
            if(!bool.TryParse(status, out bool s))
            {
                Console.WriteLine("status参数设置异常，识别类型true false");
                return;
            }

            var url = $"GETcns.api.qcloud.com/v2/index.php?Action=RecordStatus&Nonce={new Random().Next(0, 100)}&Region=&SecretId={sId}&SignatureMethod=HmacSHA256&Timestamp={Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds)}&domain={domain}&recordId={recordId}&status={(s? "enable" : "disable")}";

            var signature = System.Web.HttpUtility.UrlEncode(CreateToken(url, sKey));

            var tencentResultStr = client.GetStringAsync("https://" + url.Substring(3) + $"&Signature={signature}").Result;

            var tencentResult = JsonSerializer.Deserialize<TencentResult<object>>(tencentResultStr);
            if (tencentResult.code == 0)
            {
                Console.WriteLine($"{DateTime.Now}  {recordId}:{ (s ? "解析开启" : "解析关闭")}");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now}  设置失败:{tencentResult.message}");
            }
        }

        /// <summary>
        /// System.Text.Json时间格式处理
        /// </summary>
        public class DatetimeJsonConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    if (DateTime.TryParse(reader.GetString(), out DateTime date))
                        return date;
                }
                return reader.GetDateTime();
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                if (value.Hour == 0 && value.Minute == 0 && value.Second == 0)
                {
                    writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
                }
                else
                {
                    writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
                }

            }
        }

        /// <summary>
        /// 获取公网ip 搜狐接口
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        private static bool GetIpBySohu(out string ip)
        {
            ip = "";

            try
            {
                var result = client.GetStringAsync("http://pv.sohu.com/cityjson?ie=utf-8").Result;
                var resultSplit = result.Split('=');

                if (resultSplit.Length != 2)
                {
                    Console.WriteLine($"ip接口返回未知道结果:{result}");
                    return false;
                }

                var ipInfo = JsonSerializer.Deserialize<SohuIP>(resultSplit[1].Substring(0, resultSplit[1].Length - 1));
                ip = ipInfo.cip;
            }
            catch (Exception e)
            {
                Console.WriteLine($"ip接口异常:{e.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// HMAC SHA256 (Base64)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="secret"></param>
        /// <returns></returns>
        private static string CreateToken(string message, string secret)
        {
            var encoding = new UTF8Encoding();
            byte[] keyByte = encoding.GetBytes(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hashmessage);
            }
        }

        /// <summary>
        /// 读appsettings
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static string GetSettings(string key)
        {
            var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();
            return configuration[key];
        }

        
    }
}