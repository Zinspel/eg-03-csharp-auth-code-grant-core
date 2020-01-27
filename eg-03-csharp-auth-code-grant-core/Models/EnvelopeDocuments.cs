using System.Collections.Generic;

namespace eg_03_csharp_auth_code_grant_core.Models
{
    public class EnvelopeDocuments
    {
        public string EnvelopeId { get; set; }
        public List<EnvelopeDocItem> Documents { get; set; }
    }
}
