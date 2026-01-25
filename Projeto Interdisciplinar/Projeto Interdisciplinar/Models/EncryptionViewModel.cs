using System.ComponentModel.DataAnnotations;

namespace YourApp.Models
{
    public class EncryptionViewModel
    {
        // inputs
        public string InputText { get; set; }

        // matrix A inputs (usar mesmo campos já existentes)
        public int A11 { get; set; } = 0;
        public int A12 { get; set; } = 0;
        public int A21 { get; set; } = 0;
        public int A22 { get; set; } = 0;

        // string com os números (opcional, mantido para compatibilidade)
        public string EncodedNumbers { get; set; }

        // Matriz codificada (2 x cols) para visualização como tabela
        // Isto é apenas para apresentação — preenchida no controller.
        public int[,] EncodedMatrix { get; set; }

        // número de colunas da EncodedMatrix (0 se não existir)
        public int EncodedCols { get; set; }

        // resultado da descodificação (se aplicável)
        public string DecodedText { get; set; }

        // mensagem de erro / sucesso
        public string Message { get; set; }
    }
}

