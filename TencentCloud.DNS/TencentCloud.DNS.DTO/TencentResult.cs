using System;
using System.Collections.Generic;
using System.Text;

namespace TencentCloud.DNS.TencentCloud.DNS.DTO
{
    /// <summary>
    /// 腾讯云 DTO
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TencentResult<T> where T : class
    {
        public int code { get; set; }
        public string message { get; set; }
        public T data { get; set; }
    }
}
