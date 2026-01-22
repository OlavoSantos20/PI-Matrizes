namespace Projeto_Interdisciplinar.Models
{
    public class MatrizViewModel
    {
        public int Linhas { get; set; }
        public int Colunas { get; set; }

        // Multiplicação
        public int LinhasA { get; set; }
        public int ColunasA { get; set; }
        public int LinhasB { get; set; }
        public int ColunasB { get; set; }

        public int[,]? MatrizA { get; set; }
        public int[,]? MatrizB { get; set; }

        // Resultados
        public int[,]? Resultado { get; set; }

        public string? Operacao { get; set; }

        public int Escalar { get; set; }

        public double? Determinante { get; set; }

        public double[,]? Inversa { get; set; }
    }
}
