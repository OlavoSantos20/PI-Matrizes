using System.Xml.Serialization;

namespace Projeto_Interdisciplinar.Models
{
    public class MatrixExportModel
    {
        public string? Name { get; set; }         
        public int Rows { get; set; }
        public int Columns { get; set; }
        public double[][]? Data { get; set; }
    }

    [XmlRoot("MatrixExportModel")]
    public class MatrixExportModelXml
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string? Name { get; set; }

        [XmlArray("Data")]
        [XmlArrayItem("Row")]
        public List<MatrixRowXml> Data { get; set; } = new();
    }

    public class MatrixRowXml
    {
        [XmlElement("Value")]
        public List<double> Values { get; set; } = new();
    }

}
