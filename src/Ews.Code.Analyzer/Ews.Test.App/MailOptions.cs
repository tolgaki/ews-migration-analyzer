using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EwsTestApp
{
    public class MailOptions
    {
        public required string EmailDomain { get; set; }
        public required string SenderEmail { get; set; }
        public required string ToRecipients { get; set; }
        public required string CcRecipients { get; set; }
    }
}
