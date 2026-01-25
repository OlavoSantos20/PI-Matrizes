using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using YourApp.Models;

namespace YourApp.Controllers
{
    public class CryptoController : Controller
    {
        // ===================== TABELA =====================
        private static readonly Dictionary<char, int> CharToNum = new()
        {
            ['A'] = 1,
            ['B'] = 2,
            ['C'] = 3,
            ['D'] = 4,
            ['E'] = 5,
            ['F'] = 6,
            ['G'] = 7,
            ['H'] = 8,
            ['I'] = 9,
            ['J'] = 10,
            ['K'] = 11,
            ['L'] = 12,
            ['M'] = 13,
            ['N'] = 14,
            ['O'] = 15,
            ['P'] = 16,
            ['Q'] = 17,
            ['R'] = 18,
            ['S'] = 19,
            ['T'] = 20,
            ['U'] = 21,
            ['V'] = 22,
            ['W'] = 23,
            ['X'] = 24,
            ['Y'] = 25,
            ['Z'] = 26,
            ['.'] = 27,
            [','] = 28,
            ['-'] = 29,
            [' '] = 30
        };

        private static readonly Dictionary<int, char> NumToChar =
            CharToNum.ToDictionary(x => x.Value, x => x.Key);

        // ===================== CODIFICAR =====================
        [HttpGet]
        public IActionResult Codificar()
        {
            return View(new EncryptionViewModel());
        }

        [HttpPost]
        public IActionResult Codificar(EncryptionViewModel model)
        {
            // reconstruir matriz A e validar
            int[,] A = BuildMatrixA(model, out string error);
            if (error != null)
            {
                model.Message = error;
                return View(model);
            }

            // codificar texto
            var (encoded, err) = EncodeText(model.InputText, A);
            if (err != null)
            {
                model.Message = err;
                return View(model);
            }

            // preencher matriz 2 x cols
            int total = encoded.Count;
            int cols = total / 2;
            var mat = new int[2, cols];
            for (int k = 0; k < cols; k++)
            {
                mat[0, k] = encoded[2 * k];
                mat[1, k] = encoded[2 * k + 1];
            }

            model.EncodedMatrix = mat;
            model.EncodedCols = cols;

            // Se o utilizador clicou num botão de export, gerar e devolver ficheiro
            bool wantXml = Request.Form.ContainsKey("exportXml");
            bool wantJson = Request.Form.ContainsKey("export");

            // Nota: os botões no view têm value="Resultado" — aqui só confirmamos que existe a intenção de export
            if (wantXml || wantJson)
            {
                var exportModel = BuildExportModelFromInt(mat, "Resultado");
                if (wantXml)
                    return ExportModelAsXml(exportModel, "Resultado.xml");
                else
                    return ExportModelAsJson(exportModel, "Resultado.json");
            }

            // caso normal: devolver view com resultado apresentado em tabela
            model.Message = "Mensagem codificada com sucesso.";
            return View(model);
        }

        // ===================== DECODIFICAR (USAR MATRIZ INVERSA) =====================
        [HttpGet]
        public IActionResult Decodificar()
        {
            return View(new EncryptionViewModel());
        }

        [HttpPost]
        public IActionResult Decodificar(
    EncryptionViewModel model,
    IFormFile? matrixFile,
    string actionType)
        {
            // ================= IMPORTAR MATRIZ =================
            if (actionType == "import")
            {
                if (matrixFile == null || matrixFile.Length == 0)
                {
                    model.Message = "Seleciona um ficheiro JSON ou XML para importar.";
                    return View(model);
                }

                var importResult = ImportMatrixFromFile(matrixFile);
                if (importResult.error != null)
                {
                    model.Message = importResult.error;
                    return View(model);
                }

                model.EncodedMatrix = importResult.matrix;
                model.EncodedCols = importResult.cols;
                model.Message = "Matriz importada com sucesso.";

                return View(model);
            }

            // ================= DECODIFICAR =================
            if (actionType == "decode")
            {
                // 1️⃣ reconstruir matriz N a partir dos inputs hidden
                var (N, cols, parseError) = ParseMatrixNFromForm();
                if (parseError != null)
                {
                    model.Message = "Importa primeiro uma matriz antes de decodificar.";
                    return View(model);
                }

                model.EncodedMatrix = N;
                model.EncodedCols = cols;

                // 2️⃣ validar matriz inversa
                int[,] Binv = BuildInverseMatrix(model, out string error);
                if (error != null)
                {
                    model.Message = error;
                    return View(model);
                }

                // 3️⃣ decodificar
                var chars = new List<char>();

                for (int c = 0; c < cols; c++)
                {
                    long xVal = (long)Binv[0, 0] * N[0, c] + (long)Binv[0, 1] * N[1, c];
                    long yVal = (long)Binv[1, 0] * N[0, c] + (long)Binv[1, 1] * N[1, c];

                    if (!NumToChar.ContainsKey((int)xVal) || !NumToChar.ContainsKey((int)yVal))
                    {
                        model.Message = $"Erro na descodificação: digite a matriz inversa correta para decodificar.";
                        return View(model);
                    }

                    chars.Add(NumToChar[(int)xVal]);
                    chars.Add(NumToChar[(int)yVal]);
                }

                model.DecodedText = new string(chars.ToArray());
                model.Message = "Mensagem descodificada com sucesso.";
                return View(model);
            }

            model.Message = "Ação inválida.";
            return View(model);
        }



        // ===================== HELPERS =====================

        // Constrói a matriz A (utilizada em codificar). Valida det = ±1
        private static int[,] BuildMatrixA(EncryptionViewModel model, out string error)
        {
            error = null;
            int[,] A =
            {
                { model.A11, model.A12 },
                { model.A21, model.A22 }
            };

            int det = A[0, 0] * A[1, 1] - A[0, 1] * A[1, 0];
            if (det != 1 && det != -1)
            {
                error = $"Determinante inválido ({det}). A matriz A deve ter determinante ±1.";
                return null;
            }

            return A;
        }

        // Constrói a matriz que o utilizador fornece para descodificar — ASSUMIMOS que é a inversa (B).
        private static int[,] BuildInverseMatrix(EncryptionViewModel model, out string error)
        {
            error = null;
            int[,] B =
            {
                { model.A11, model.A12 },
                { model.A21, model.A22 }
            };

            int detB = B[0, 0] * B[1, 1] - B[0, 1] * B[1, 0];
            if (detB != 1 && detB != -1)
            {
                error = $"Erro na descodificação: digite a matriz inversa correta para decodificar.";
                return null;
            }

            return B;
        }

        // --- Parse da matriz N (2 x cols) a partir do Request.Form
        // Procura chaves do tipo "N[r,c]" e infere cols = max c + 1.
        private (int[,] matrix, int cols, string error) ParseMatrixNFromForm()
        {
            var pattern = new Regex(@"^N\[(\d+),(\d+)\]");
            int maxC = -1;
            int maxR = -1;
            foreach (var key in Request.Form.Keys)
            {
                var m = pattern.Match(key);
                if (m.Success)
                {
                    if (int.TryParse(m.Groups[1].Value, out int r) && int.TryParse(m.Groups[2].Value, out int c))
                    {
                        if (r > maxR) maxR = r;
                        if (c > maxC) maxC = c;
                    }
                }
            }

            if (maxC < 0 || maxR < 0)
                return (null, 0, "Não foram encontradas entradas da matriz codificada (use inputs com nome N[0,col] e N[1,col]).");

            if (maxR > 1)
                return (null, 0, "A matriz codificada deve ter exactamente 2 linhas (nomes N[0,c] e N[1,c]).");

            int cols = maxC + 1;
            var mat = new int[2, cols];

            for (int r = 0; r <= maxR; r++)
            {
                for (int c = 0; c <= maxC; c++)
                {
                    string key = $"N[{r},{c}]";
                    if (Request.Form.ContainsKey(key))
                    {
                        var sval = Request.Form[key].ToString();
                        if (!int.TryParse(sval, out int v))
                        {
                            return (null, 0, $"Valor inválido na chave {key}: '{sval}'");
                        }
                        mat[r, c] = v;
                    }
                    else
                    {
                        mat[r, c] = 0;
                    }
                }
            }

            return (mat, cols, null);
        }

        private static (List<int>, string) EncodeText(string text, int[,] A)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (null, "Mensagem vazia.");

            text = text.ToUpperInvariant();
            var nums = new List<int>();

            foreach (char c in text)
            {
                if (!CharToNum.ContainsKey(c))
                    return (null, $"Carácter inválido: {c}");
                nums.Add(CharToNum[c]);
            }

            if (nums.Count % 2 != 0)
                nums.Add(CharToNum[' ']);

            int cols = nums.Count / 2;
            var result = new List<int>();

            for (int i = 0; i < cols; i++)
            {
                int x = nums[2 * i];
                int y = nums[2 * i + 1];

                result.Add(A[0, 0] * x + A[0, 1] * y);
                result.Add(A[1, 0] * x + A[1, 1] * y);
            }

            return (result, null);
        }

        // ===================== EXPORT HELPERS =====================
        // Modelo simples para exportação (serializável em JSON e XML)
        public class MatrixExportModel
        {

            public int Rows { get; set; }
            public int Columns { get; set; }
            public double[][] Data { get; set; }
            public string Name { get; set; }
        }

        private static MatrixExportModel BuildExportModelFromInt(int[,] mat, string name)
        {
            int rows = mat.GetLength(0);
            int cols = mat.GetLength(1);
            var data = new double[rows][];
            for (int r = 0; r < rows; r++)
            {
                data[r] = new double[cols];
                for (int c = 0; c < cols; c++)
                    data[r][c] = mat[r, c];
            }

            return new MatrixExportModel
            {
                Rows = rows,
                Columns = cols,
                Data = data,
                Name = name
            };
        }

        private IActionResult ExportModelAsJson(MatrixExportModel model, string filename)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(model, options);
            return File(bytes, "application/json", filename);
        }

        private IActionResult ExportModelAsXml(MatrixExportModel model, string filename)
        {
            var xmlSerializer = new XmlSerializer(typeof(MatrixExportModel));
            using var ms = new System.IO.MemoryStream();
            // Serializar para UTF-8
            using (var writer = new System.IO.StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                xmlSerializer.Serialize(writer, model);
            }
            ms.Position = 0;
            return File(ms.ToArray(), "application/xml", filename);
        }


        private (int[,] matrix, int cols, string? error) ImportMatrixFromFile(IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();

                MatrixExportModel model;

                if (file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    model = JsonSerializer.Deserialize<MatrixExportModel>(stream);
                }
                else if (file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var serializer = new XmlSerializer(typeof(MatrixExportModel));
                    model = (MatrixExportModel)serializer.Deserialize(stream);
                }
                else
                {
                    return (null, 0, "Formato de ficheiro não suportado.");
                }

                if (model?.Data == null || model.Rows != 2)
                    return (null, 0, "A matriz importada tem de ter 2 linhas.");

                int cols = model.Columns;
                var mat = new int[2, cols];

                for (int r = 0; r < 2; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        mat[r, c] = (int)model.Data[r][c];
                    }
                }

                return (mat, cols, null);
            }
            catch
            {
                return (null, 0, "Erro ao ler o ficheiro de matriz.");
            }
        }

    }
}
