using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeGeneric.Models
{
    public class ColumnMetadata
    {
        public string TableName { get; set; }
        public string ColumnName { get; set; }

        public string AllowedValues { get; set; }
        public string Regex { get; set; }
    }

}
