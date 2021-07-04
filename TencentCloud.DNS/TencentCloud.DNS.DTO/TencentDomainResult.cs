using System;
using System.Collections.Generic;
using System.Text;

namespace TencentCloud.DNS.TencentCloud.DNS.DTO
{
    /// <summary>
    /// 腾讯云 获取域名列表 DTO
    /// </summary>
    public class TencentDomainResult
    {
        public Info info { get; set; }
        public List<Domain> domains { get; set; }
        public List<Record> records { get; set; }

        public class Info
        {
            public string sub_domains { get; set; }
            public string record_total { get; set; }
        }

        public class Domain
        {
            public long id { get; set; }
            public string name { get; set; }
            public string status { get; set; }
            public string ttl { get; set; }
            public DateTime created_on { get; set; }
            public DateTime updated_on { get; set; }
        }

        public class Record
        {
            public long id { get; set; }
            public string value { get; set; }
            public string name { get; set; }
            public DateTime updated_on { get; set; }
        }
    }
}
