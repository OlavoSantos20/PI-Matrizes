using Microsoft.AspNetCore.Mvc;
using Projeto_Interdisciplinar.Models;
using System;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.IO;
using Microsoft.AspNetCore.Http;


public class MatrizesController : Controller
{
    [HttpGet]
    public IActionResult SomaSubtracao()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult SomaSubtracao(MatrizViewModel model, string? operacao, string? import, IFormFile? importFile)
    {
        if (model == null)
            model = new MatrizViewModel();

        // Limpar validação automática dos campos que tratamos manualmente
        ModelState.Remove(nameof(model.Linhas));
        ModelState.Remove(nameof(model.Colunas));
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.MatrizB));
        ModelState.Remove(nameof(model.Resultado));
        ModelState.Remove(nameof(model.Operacao));

        // --- Reconstruir matrizes do Request.Form se o utilizador submeteu a grelha ---
        // Isso preserva valores já preenchidos na página quando o utilizador clica noutro botão (ex: importar)
        if (Request.Form.Keys.Any(k => k.StartsWith("MatrizA[", StringComparison.OrdinalIgnoreCase)))
        {
            // Inferir linhas/colunas se necessário (usar model se já tiver)
            int rowsA = model.Linhas > 0 ? model.Linhas : InferRowsFromFormKeys("MatrizA");
            int colsA = model.Colunas > 0 ? model.Colunas : InferColsFromFormKeys("MatrizA");
            if (rowsA > 0 && colsA > 0)
            {
                model.MatrizA = ParseIntMatrixFromForm("MatrizA", rowsA, colsA);
                model.Linhas = rowsA;
                model.Colunas = colsA;
            }
        }

        if (Request.Form.Keys.Any(k => k.StartsWith("MatrizB[", StringComparison.OrdinalIgnoreCase)))
        {
            int rowsB = model.Linhas > 0 ? model.Linhas : InferRowsFromFormKeys("MatrizB");
            int colsB = model.Colunas > 0 ? model.Colunas : InferColsFromFormKeys("MatrizB");
            if (rowsB > 0 && colsB > 0)
            {
                model.MatrizB = ParseIntMatrixFromForm("MatrizB", rowsB, colsB);
                model.Linhas = rowsB;
                model.Colunas = colsB;
            }
        }

        // ----------------- IMPORT (JSON ou XML) --------------------------------
        if (!string.IsNullOrEmpty(import) && importFile != null)
        {
            if (importFile.Length == 0)
            {
                ModelState.AddModelError("", "Ficheiro vazio.");
                return View(model);
            }

            try
            {
                using var ms = new MemoryStream();
                importFile.CopyTo(ms);
                var bytes = ms.ToArray();

                // Detectar XML pelo nome do ficheiro ou pelo conteúdo inicial
                bool likelyXml = importFile.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                                 || (bytes.Length > 0 && (bytes[0] == (byte)'<' || bytes[0] == (byte)0xEF)); // 0xEF para BOM UTF-8

                MatrixExportModel exportModel = null;

                if (likelyXml)
                {
                    if (!TryDeserializeExportModelFromXml(bytes, out exportModel))
                    {
                        ModelState.AddModelError("", "Formato XML inválido ou mal formado.");
                        return View(model);
                    }
                }
                else
                {
                    // JSON tradicional
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    exportModel = JsonSerializer.Deserialize<MatrixExportModel>(bytes, options);
                    if (exportModel == null || exportModel.Data == null)
                    {
                        ModelState.AddModelError("", "Formato JSON inválido.");
                        return View(model);
                    }
                }

                // Validar dimensão básica
                int rows = exportModel.Rows;
                int cols = exportModel.Columns;
                if (rows < 1 || cols < 1 || rows > 30 || cols > 30)
                {
                    ModelState.AddModelError("", "Dimensões no ficheiro fora do permitido (1..30).");
                    return View(model);
                }

                // validar shape
                if (exportModel.Data.Length != rows)
                {
                    ModelState.AddModelError("", "O número de linhas do 'Data' não corresponde a 'Rows'.");
                    return View(model);
                }
                for (int r = 0; r < rows; r++)
                {
                    if (exportModel.Data[r] == null || exportModel.Data[r].Length != cols)
                    {
                        ModelState.AddModelError("", $"A linha {r} do 'Data' não tem {cols} colunas.");
                        return View(model);
                    }
                }

                // --- Regras específicas para manter A quando se importa B (mesmo comportamento anterior) ---
                bool hasA = model.MatrizA != null && model.MatrizA.Length > 0;
                bool hasDimensions = model.Linhas > 0 && model.Colunas > 0;

                if (string.Equals(import, "MatrizA", StringComparison.OrdinalIgnoreCase))
                {
                    model.Linhas = rows;
                    model.Colunas = cols;
                    model.MatrizA = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizA[i, j] = Convert.ToInt32(Math.Round(exportModel.Data[i][j]));

                    // Se MatrizB não tiver dimensões definidas, adoptar as mesmas dimensões e inicializar zeros
                    if (model.LinhasB <= 0 && model.ColunasB <= 0 && model.MatrizB == null)
                    {
                        model.LinhasB = rows;
                        model.ColunasB = cols;
                        model.MatrizB = new int[rows, cols];
                    }

                    return View(model);
                }
                else if (string.Equals(import, "MatrizB", StringComparison.OrdinalIgnoreCase))
                {
                    // Se já existir MatrizA — obrigamos match de dimensões
                    if (hasA)
                    {
                        int aRows = model.MatrizA.GetLength(0);
                        int aCols = model.MatrizA.GetLength(1);
                        if (aRows != rows || aCols != cols)
                        {
                            ModelState.AddModelError("", "A Matriz B tem dimensões diferentes da Matriz A — importação cancelada.");
                            return View(model);
                        }
                    }
                    else if (hasDimensions)
                    {
                        // Se temos dimensões definidas (user clicou em Criar Matrizes) obrigamos match com essas dimensões
                        if (model.Linhas != rows || model.Colunas != cols)
                        {
                            ModelState.AddModelError("", "A Matriz B tem dimensões diferentes das dimensões actuais definidas — importação cancelada.");
                            return View(model);
                        }
                    }
                    else
                    {
                        // Não existe A nem dimensões definidas — aceitar B e adoptar as dimensões do ficheiro
                        model.Linhas = rows;
                        model.Colunas = cols;
                    }

                    // Gravar MatrizB (sem tocar em MatrizA)
                    model.MatrizB = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizB[i, j] = Convert.ToInt32(Math.Round(exportModel.Data[i][j]));

                    return View(model);
                }
                else
                {
                    ModelState.AddModelError("", "Botão de import desconhecido.");
                    return View(model);
                }
            }
            catch (JsonException)
            {
                ModelState.AddModelError("", "Erro ao desserializar o ficheiro JSON.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro a processar o ficheiro: " + ex.Message);
                return View(model);
            }
        }

        // ----------------- EXPORT (JSON existente) ou XML (novo) ------------------
        if (Request.Form.ContainsKey("export") || Request.Form.ContainsKey("exportXml"))
        {
            // qual matriz/resultado exportar (valor do botão)
            var which = Request.Form.ContainsKey("export") ? Request.Form["export"].ToString() : Request.Form["exportXml"].ToString();
            bool wantXml = Request.Form.ContainsKey("exportXml");

            // função small para obter rows/cols e validar/inferir como antes
            int InferRowsColsFromFormOrModel(string prefix, ref int rows, ref int cols)
            {
                // tentamos usar model.Linhas/model.Colunas (no seu código há um único pares Linhas/Colunas para ambas)
                if (rows <= 0 || cols <= 0)
                {
                    int maxR = -1, maxC = -1;
                    var pattern = new System.Text.RegularExpressions.Regex($@"^{prefix}\[(\d+),(\d+)\]");
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
                    if (maxR >= 0 && maxC >= 0)
                    {
                        rows = maxR + 1;
                        cols = maxC + 1;
                    }
                }
                return 0;
            }

            if (string.Equals(which, "MatrizA", StringComparison.OrdinalIgnoreCase))
            {
                int rows = model.Linhas;
                int cols = model.Colunas;
                if (rows <= 0 || cols <= 0)
                {
                    InferRowsColsFromFormOrModel("MatrizA", ref rows, ref cols);
                    if (rows <= 0 || cols <= 0)
                    {
                        ModelState.AddModelError("", "Não foi possível inferir o tamanho da MatrizA para exportação. Define Linhas/Colunas ou preenche a grelha.");
                        return View(model);
                    }
                }

                var matA = ParseIntMatrixFromForm("MatrizA", rows, cols);
                var exportModel = BuildExportModelFromInt(matA, "MatrizA");

                if (wantXml)
                    return ExportModelAsXml(exportModel, "MatrizA.xml");
                else
                    return ExportModelAsJson(exportModel);
            }

            if (string.Equals(which, "MatrizB", StringComparison.OrdinalIgnoreCase))
            {
                int rows = model.Linhas;
                int cols = model.Colunas;
                if (rows <= 0 || cols <= 0)
                {
                    InferRowsColsFromFormOrModel("MatrizB", ref rows, ref cols);
                    if (rows <= 0 || cols <= 0)
                    {
                        ModelState.AddModelError("", "Não foi possível inferir o tamanho da MatrizB para exportação. Define Linhas/Colunas ou preenche a grelha.");
                        return View(model);
                    }
                }

                var matB = ParseIntMatrixFromForm("MatrizB", rows, cols);
                var exportModel = BuildExportModelFromInt(matB, "MatrizB");

                if (wantXml)
                    return ExportModelAsXml(exportModel, "MatrizB.xml");
                else
                    return ExportModelAsJson(exportModel);
            }          
        }

        // --- 3) Validação de dimensões ---
        // Só validar quando NÃO foi um pedido de import
        if (string.IsNullOrEmpty(import))
        {
            if (model.Linhas < 1 || model.Colunas < 1 ||
                model.Linhas > 30 || model.Colunas > 30)
            {
                ModelState.AddModelError("", "Dimensões inválidas. Linhas e colunas entre 1 e 30.");
                return View(model);
            }
        }


        // Se o utilizador só clicou em "Criar Matrizes"
        if (string.IsNullOrEmpty(operacao))
        {
            // NÃO inicializar matrizes → inputs ficam vazios
            return View(model);
        }

        int linhas = model.Linhas;
        int colunas = model.Colunas;

        // Inicializar matrizes (garantir que existem para leitura do form)
        model.MatrizA ??= new int[linhas, colunas];
        model.MatrizB ??= new int[linhas, colunas];
        model.Resultado = new int[linhas, colunas];

        // Preencher MatrizA e MatrizB com os valores do form
        for (int i = 0; i < linhas; i++)
        {
            for (int j = 0; j < colunas; j++)
            {
                string keyA = $"MatrizA[{i},{j}]";
                string keyB = $"MatrizB[{i},{j}]";

                if (Request.Form.ContainsKey(keyA) && int.TryParse(Request.Form[keyA], out int valA))
                    model.MatrizA[i, j] = valA;
                else
                    model.MatrizA[i, j] = 0;

                if (Request.Form.ContainsKey(keyB) && int.TryParse(Request.Form[keyB], out int valB))
                    model.MatrizB[i, j] = valB;
                else
                    model.MatrizB[i, j] = 0;
            }
        }

        // Calcular resultado
        for (int i = 0; i < linhas; i++)
        {
            for (int j = 0; j < colunas; j++)
            {
                model.Resultado[i, j] = string.Equals(operacao, "soma", StringComparison.OrdinalIgnoreCase)
                    ? model.MatrizA[i, j] + model.MatrizB[i, j]
                    : model.MatrizA[i, j] - model.MatrizB[i, j];
            }
        }

        return View(model);
    }


   


    [HttpGet]
    public IActionResult MultiplicacaoMatriz()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult MultiplicacaoMatriz(MatrizViewModel model, string? operacao, string? import, IFormFile? importFile)
    {
        if (model == null)
            model = new MatrizViewModel();

        // limpar validação automática para campos tratados manualmente
        ModelState.Remove(nameof(model.LinhasA));
        ModelState.Remove(nameof(model.ColunasA));
        ModelState.Remove(nameof(model.LinhasB));
        ModelState.Remove(nameof(model.ColunasB));
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.MatrizB));
        ModelState.Remove(nameof(model.Resultado));
        ModelState.Remove(nameof(model.Operacao));

        // --- Reconstruir matrizes a partir do form (preservar valores quando o utilizador clica noutro botão) ---
        // Remover eventuais chaves antigas do ModelState para que a reconstrução use os valores do model depois
        foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("MatrizA[", StringComparison.OrdinalIgnoreCase)).ToList())
            ModelState.Remove(key);
        foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("MatrizB[", StringComparison.OrdinalIgnoreCase)).ToList())
            ModelState.Remove(key);

        if (Request.Form.Keys.Any(k => k.StartsWith("MatrizA[", StringComparison.OrdinalIgnoreCase)))
        {
            // preferir valores model.LinhasA/ColunasA se existirem, senão inferir das keys
            int rowsA1 = model.LinhasA > 0 ? model.LinhasA : InferRowsFromFormKeys("MatrizA");
            int colsA1 = model.ColunasA > 0 ? model.ColunasA : InferColsFromFormKeys("MatrizA");
            if (rowsA1 > 0 && colsA1 > 0)
            {
                model.MatrizA = ParseIntMatrixFromForm("MatrizA", rowsA1, colsA1);
                model.LinhasA = rowsA1;
                model.ColunasA = colsA1;
            }
        }

        if (Request.Form.Keys.Any(k => k.StartsWith("MatrizB[", StringComparison.OrdinalIgnoreCase)))
        {
            int rowsB1 = model.LinhasB > 0 ? model.LinhasB : InferRowsFromFormKeys("MatrizB");
            int colsB1 = model.ColunasB > 0 ? model.ColunasB : InferColsFromFormKeys("MatrizB");
            if (rowsB1 > 0 && colsB1 > 0)
            {
                model.MatrizB = ParseIntMatrixFromForm("MatrizB", rowsB1, colsB1);
                model.LinhasB = rowsB1;
                model.ColunasB = colsB1;
            }
        }

        // --- 1) IMPORT server-side (Importar MatrizA ou MatrizB via ficheiro JSON) ---
        if (!string.IsNullOrEmpty(import) && importFile != null)
        {
            if (importFile.Length == 0)
            {
                ModelState.AddModelError("", "Ficheiro vazio.");
                return View(model);
            }

            try
            {
                using var ms = new MemoryStream();
                importFile.CopyTo(ms);
                var bytes = ms.ToArray();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var importModel = JsonSerializer.Deserialize<MatrixExportModel>(bytes, options);

                if (importModel == null || importModel.Data == null)
                {
                    ModelState.AddModelError("", "Formato JSON inválido.");
                    return View(model);
                }

                int rows = importModel.Rows;
                int cols = importModel.Columns;
                if (rows < 1 || cols < 1 || rows > 30 || cols > 30)
                {
                    ModelState.AddModelError("", "Dimensões no ficheiro fora do permitido (1..30).");
                    return View(model);
                }

                if (importModel.Data.Length != rows)
                {
                    ModelState.AddModelError("", "O número de linhas do 'Data' não corresponde a 'Rows'.");
                    return View(model);
                }
                for (int r = 0; r < rows; r++)
                {
                    if (importModel.Data[r] == null || importModel.Data[r].Length != cols)
                    {
                        ModelState.AddModelError("", $"A linha {r} do 'Data' não tem {cols} colunas.");
                        return View(model);
                    }
                }

                // Importar Matriz A
                if (string.Equals(import, "MatrizA", StringComparison.OrdinalIgnoreCase))
                {
                    model.LinhasA = rows;
                    model.ColunasA = cols;
                    model.MatrizA = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizA[i, j] = Convert.ToInt32(Math.Round(importModel.Data[i][j]));

                    // remover possíveis entradas anteriores do ModelState para as keys MatrizA[...]
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            ModelState.Remove($"MatrizA[{i},{j}]");

                    // Se MatrizB não tiver dimensões definidas, definir com o mesmo tamanho e inicializar a zeros
                    if (model.LinhasB <= 0 || model.ColunasB <= 0)
                    {
                        model.LinhasB = rows;
                        model.ColunasB = cols;
                        model.MatrizB = new int[rows, cols]; // zeros por defeito
                        for (int i = 0; i < rows; i++)
                            for (int j = 0; j < cols; j++)
                                ModelState.Remove($"MatrizB[{i},{j}]"); // garantir que a grelha B aparece limpa
                    }

                    return View(model);
                }

                // Importar Matriz B
                if (string.Equals(import, "MatrizB", StringComparison.OrdinalIgnoreCase))
                {
                    model.LinhasB = rows;
                    model.ColunasB = cols;
                    model.MatrizB = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizB[i, j] = Convert.ToInt32(Math.Round(importModel.Data[i][j]));

                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            ModelState.Remove($"MatrizB[{i},{j}]");

                    // Se MatrizA não tiver dimensões definidas, definir com o mesmo tamanho e inicializar a zeros
                    if (model.LinhasA <= 0 || model.ColunasA <= 0)
                    {
                        model.LinhasA = rows;
                        model.ColunasA = cols;
                        model.MatrizA = new int[rows, cols]; // zeros por defeito
                        for (int i = 0; i < rows; i++)
                            for (int j = 0; j < cols; j++)
                                ModelState.Remove($"MatrizA[{i},{j}]"); // garantir que a grelha A aparece limpa
                    }

                    return View(model);
                }

                ModelState.AddModelError("", "Botão de import desconhecido.");
                return View(model);
            }
            catch (JsonException)
            {
                ModelState.AddModelError("", "Erro ao desserializar o ficheiro JSON.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro a processar o ficheiro: " + ex.Message);
                return View(model);
            }
        }


        // --- 2) EXPORT (se pedido) ---
        if (Request.Form.ContainsKey("export"))
        {
            var which = Request.Form["export"].ToString();

            if (string.Equals(which, "MatrizA", StringComparison.OrdinalIgnoreCase))
            {
                int rows = model.LinhasA;
                int cols = model.ColunasA;
                if (rows <= 0 || cols <= 0)
                {
                    ModelState.AddModelError("", "Define as dimensões da Matriz A antes de exportar.");
                    return View(model);
                }

                var matA = ParseIntMatrixFromForm("MatrizA", rows, cols);
                var exportModel = BuildExportModelFromInt(matA, "MatrizA");
                return ExportModelAsJson(exportModel);
            }

            if (string.Equals(which, "MatrizB", StringComparison.OrdinalIgnoreCase))
            {
                int rows = model.LinhasB;
                int cols = model.ColunasB;
                if (rows <= 0 || cols <= 0)
                {
                    ModelState.AddModelError("", "Define as dimensões da Matriz B antes de exportar.");
                    return View(model);
                }

                var matB = ParseIntMatrixFromForm("MatrizB", rows, cols);
                var exportModel = BuildExportModelFromInt(matB, "MatrizB");
                return ExportModelAsJson(exportModel);
            }

            if (string.Equals(which, "Resultado", StringComparison.OrdinalIgnoreCase))
            {
                // Precisamos das dimensões para calcular resultado
                if (model.LinhasA <= 0 || model.ColunasA <= 0 || model.LinhasB <= 0 || model.ColunasB <= 0)
                {
                    ModelState.AddModelError("", "Define as dimensões de ambas as matrizes antes de exportar o Resultado.");
                    return View(model);
                }

                // Validar possibilidade de multiplicação
                if (model.ColunasA != model.LinhasB)
                {
                    ModelState.AddModelError("", "Para calcular o resultado é necessário que Nº Colunas(A) = Nº Linhas(B).");
                    return View(model);
                }

                var A = ParseIntMatrixFromForm("MatrizA", model.LinhasA, model.ColunasA);
                var B = ParseIntMatrixFromForm("MatrizB", model.LinhasB, model.ColunasB);

                int rowsR = model.LinhasA;
                int colsR = model.ColunasB;
                var R = new int[rowsR, colsR];

                for (int i = 0; i < rowsR; i++)
                {
                    for (int j = 0; j < colsR; j++)
                    {
                        long sum = 0;
                        for (int k = 0; k < model.ColunasA; k++)
                            sum += (long)A[i, k] * B[k, j];

                        R[i, j] = (int)sum;
                    }
                }

                var exportModel = BuildExportModelFromInt(R, "Resultado");
                return ExportModelAsJson(exportModel);
            }
        }

        // --- 3) Validações antes de executar multiplicação ---
        if (model.LinhasA < 1 || model.ColunasA < 1 ||
            model.LinhasB < 1 || model.ColunasB < 1 ||
            model.LinhasA > 30 || model.ColunasA > 30 ||
            model.LinhasB > 30 || model.ColunasB > 30)
        {
            ModelState.AddModelError("", "Dimensões inválidas. Valores entre 1 e 30 para todas as dimensões.");
            return View(model);
        }

        // Se o utilizador só clicou em "Criar Matrizes" (operacao vazio), renderiza grelha vazia
        if (string.IsNullOrEmpty(operacao))
        {
            return View(model);
        }

        // Só executa multiplicação quando o botão "multiplicar" foi clicado
        if (!string.Equals(operacao, "multiplicar", StringComparison.OrdinalIgnoreCase))
        {
            return View(model);
        }

        // Verificar se as dimensões permitem multiplicação: ColunasA == LinhasB
        if (model.ColunasA != model.LinhasB)
        {
            ModelState.AddModelError("", "Para multiplicar as matrizes é preciso Nº Colunas(A) = Nº Linhas(B)");
            return View(model);
        }

        int linhasA = model.LinhasA;
        int colsA = model.ColunasA;
        int linhasB = model.LinhasB;
        int colsB = model.ColunasB;

        model.Operacao = operacao;

        // Inicializar matrizes para leitura e resultado (garantir que existem)
        model.MatrizA ??= new int[linhasA, colsA];
        model.MatrizB ??= new int[linhasB, colsB];
        model.Resultado = new int[linhasA, colsB];

        // Ler MatrizA do Request.Form (fallback 0 quando vazio/inválido)
        for (int i = 0; i < linhasA; i++)
        {
            for (int j = 0; j < colsA; j++)
            {
                string keyA = $"MatrizA[{i},{j}]";
                if (Request.Form.ContainsKey(keyA) && int.TryParse(Request.Form[keyA], out int valA))
                    model.MatrizA[i, j] = valA;
                else
                    model.MatrizA[i, j] = 0;
            }
        }

        // Ler MatrizB do Request.Form
        for (int i = 0; i < linhasB; i++)
        {
            for (int j = 0; j < colsB; j++)
            {
                string keyB = $"MatrizB[{i},{j}]";
                if (Request.Form.ContainsKey(keyB) && int.TryParse(Request.Form[keyB], out int valB))
                    model.MatrizB[i, j] = valB;
                else
                    model.MatrizB[i, j] = 0;
            }
        }

        // Multiplicação: Resultado (linhasA x colsB) = MatrizA (linhasA x colsA) * MatrizB (linhasB x colsB)
        for (int i = 0; i < linhasA; i++)
        {
            for (int j = 0; j < colsB; j++)
            {
                long sum = 0;
                for (int k = 0; k < colsA; k++)
                {
                    sum += (long)model.MatrizA[i, k] * model.MatrizB[k, j];
                }

                model.Resultado[i, j] = (int)sum;
            }
        }

        return View(model);
    }




    [HttpGet]
    public IActionResult MultiplicacaoEscalar()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult MultiplicacaoEscalar(MatrizViewModel model, string? operacao, string? import, IFormFile? importFile)
    {

        if (model == null)
            model = new MatrizViewModel();

        // 1️⃣ limpar ModelState
        ModelState.Remove(nameof(model.Linhas));
        ModelState.Remove(nameof(model.Colunas));
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.MatrizB));
        ModelState.Remove(nameof(model.Resultado));
        ModelState.Remove(nameof(model.Operacao));
        ModelState.Remove(nameof(model.Escalar));

        // --- 1) IMPORT server-side: se o botão "import" foi clicado e existe um ficheiro ---
        if (!string.IsNullOrEmpty(import) && importFile != null)
        {
            // validar tamanho / tipo básico (opcional)
            if (importFile.Length == 0)
            {
                ModelState.AddModelError("", "Ficheiro vazio.");
                return View(model);
            }

            try
            {
                using var ms = new MemoryStream();
                importFile.CopyTo(ms);
                var bytes = ms.ToArray();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var exportModel = JsonSerializer.Deserialize<MatrixExportModel>(bytes, options);

                if (exportModel == null || exportModel.Data == null)
                {
                    ModelState.AddModelError("", "Formato JSON inválido.");
                    return View(model);
                }

                int rows = exportModel.Rows;
                int cols = exportModel.Columns;

                if (rows < 1 || cols < 1 || rows > 30 || cols > 30)
                {
                    ModelState.AddModelError("", "Dimensões no ficheiro fora do permitido (1..30).");
                    return View(model);
                }

                // validar shape
                if (exportModel.Data.Length != rows)
                {
                    ModelState.AddModelError("", "O número de linhas do 'Data' não corresponde a 'Rows'.");
                    return View(model);
                }
                for (int r = 0; r < rows; r++)
                {
                    if (exportModel.Data[r] == null || exportModel.Data[r].Length != cols)
                    {
                        ModelState.AddModelError("", $"A linha {r} do 'Data' não tem {cols} colunas.");
                        return View(model);
                    }
                }

                model.Linhas = rows;
                model.Colunas = cols;

                if (import == "MatrizA")
                {
                    model.MatrizA = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizA[i, j] = Convert.ToInt32(Math.Round(exportModel.Data[i][j]));
                }

                // devolve view com grelha preenchida
                return View(model);
            }
            catch (JsonException)
            {
                ModelState.AddModelError("", "Erro ao desserializar o ficheiro JSON.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro a processar o ficheiro: " + ex.Message);
                return View(model);
            }
        }

        if (model.Linhas < 1 || model.Colunas < 1 ||
            model.Linhas > 30 || model.Colunas > 30)
        {
            ModelState.AddModelError("", "Dimensões inválidas (1 a 30).");
            return View(model);
        }

        // Detectar pedido de exportação (botão name="export")
        if (Request.Form.ContainsKey("export"))
        {
            var which = Request.Form["export"].ToString(); 

            // Exportar MatrizA
            if (which == "MatrizA")
            {
                var matA = ParseIntMatrixFromForm("MatrizA", model.Linhas, model.Colunas);
                var exportModel = BuildExportModelFromInt(matA, "MatrizA");
                return ExportModelAsJson(exportModel);
            }           

           
        }

        // Se o utilizador só clicou em "Criar Matrizes"
        if (string.IsNullOrEmpty(operacao))
        {
            // NÃO inicializar matrizes → inputs ficam vazios
            return View(model);
        }

        int linhas = model.Linhas;
        int colunas = model.Colunas;

        model.Operacao = operacao;

        model.MatrizA = new int[linhas, colunas];
        model.Resultado = new int[linhas, colunas];

        // Ler Matriz A
        for (int i = 0; i < linhas; i++)
        {
            for (int j = 0; j < colunas; j++)
            {
                string keyA = $"MatrizA[{i},{j}]";

                if (Request.Form.ContainsKey(keyA) &&
                    int.TryParse(Request.Form[keyA], out int valA))
                    model.MatrizA[i, j] = valA;
            }
        }

        // Escalar agora vem do Model
        int escalar = model.Escalar;

        // Multiplicação escalar
        for (int i = 0; i < linhas; i++)
        {
            for (int j = 0; j < colunas; j++)
            {
                model.Resultado[i, j] = model.MatrizA[i, j] * escalar;
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Determinante()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult Determinante(MatrizViewModel model, string? operacao, string? import, IFormFile? importFile)
    {
        if (model == null)
            model = new MatrizViewModel();

        // 1️⃣ limpar ModelState
        ModelState.Remove(nameof(model.Linhas));
        ModelState.Remove(nameof(model.Colunas));
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.MatrizB));
        ModelState.Remove(nameof(model.Resultado));
        ModelState.Remove(nameof(model.Operacao));
        ModelState.Remove(nameof(model.Escalar));

        // --- 1) IMPORT server-side: se o botão "import" foi clicado e existe um ficheiro ---
        if (!string.IsNullOrEmpty(import) && importFile != null)
        {
            // validar tamanho / tipo básico (opcional)
            if (importFile.Length == 0)
            {
                ModelState.AddModelError("", "Ficheiro vazio.");
                return View(model);
            }

            try
            {
                using var ms = new MemoryStream();
                importFile.CopyTo(ms);
                var bytes = ms.ToArray();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var exportModel = JsonSerializer.Deserialize<MatrixExportModel>(bytes, options);

                if (exportModel == null || exportModel.Data == null)
                {
                    ModelState.AddModelError("", "Formato JSON inválido.");
                    return View(model);
                }

                int rows = exportModel.Rows;
                int cols = exportModel.Columns;

                if (rows < 1 || cols < 1 || rows > 30 || cols > 30)
                {
                    ModelState.AddModelError("", "Dimensões no ficheiro fora do permitido (1..30).");
                    return View(model);
                }

                if (rows != cols)
                {
                    ModelState.AddModelError("", "A matriz tem de ser quadrada para calcular o determinante.");
                    return View(model);
                }

                // validar shape
                if (exportModel.Data.Length != rows)
                {
                    ModelState.AddModelError("", "O número de linhas do 'Data' não corresponde a 'Rows'.");
                    return View(model);
                }
                for (int r = 0; r < rows; r++)
                {
                    if (exportModel.Data[r] == null || exportModel.Data[r].Length != cols)
                    {
                        ModelState.AddModelError("", $"A linha {r} do 'Data' não tem {cols} colunas.");
                        return View(model);
                    }
                }

                model.Linhas = rows;
                model.Colunas = cols;

                if (import == "MatrizA")
                {
                    model.MatrizA = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizA[i, j] = Convert.ToInt32(Math.Round(exportModel.Data[i][j]));
                }

                // devolve view com grelha preenchida
                return View(model);
            }
            catch (JsonException)
            {
                ModelState.AddModelError("", "Erro ao desserializar o ficheiro JSON.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro a processar o ficheiro: " + ex.Message);
                return View(model);
            }
        }

        // Se Colunas não foi submetido (a view usa apenas "Linhas"), assume quadrado
        if (model.Colunas == 0 && model.Linhas > 0)
            model.Colunas = model.Linhas;

        // Validar dimensões
        if (model.Linhas < 1 || model.Colunas < 1 ||
            model.Linhas > 30 || model.Colunas > 30)
        {
            ModelState.AddModelError("", "Dimensões inválidas (1 a 30).");
            return View(model);
        }


        // Só faz sentido para matriz quadrada
        if (model.Linhas != model.Colunas)
        {
            ModelState.AddModelError("", "A matriz tem de ser quadrada para calcular o determinante.");
            return View(model);
        }

        // Detectar pedido de exportação (botão name="export")
        if (Request.Form.ContainsKey("export"))
        {
            var which = Request.Form["export"].ToString();

            // Exportar MatrizA
            if (which == "MatrizA")
            {
                var matA = ParseIntMatrixFromForm("MatrizA", model.Linhas, model.Colunas);
                var exportModel = BuildExportModelFromInt(matA, "MatrizA");
                return ExportModelAsJson(exportModel);
            }                     
        }


        int n = model.Linhas;
        model.Operacao = operacao;

        bool hasMatrixInputs = Request.Form.Keys.Any(k => k.StartsWith("MatrizA["));

        if (!string.Equals(operacao, "determinante", StringComparison.OrdinalIgnoreCase))
        {
            return View(model);
        }

        model.MatrizA = new int[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                string keyA = $"MatrizA[{i},{j}]";

                if (Request.Form.ContainsKey(keyA) &&
                    int.TryParse(Request.Form[keyA], out int valA))
                {
                    model.MatrizA[i, j] = valA;
                }
                else
                {
                    model.MatrizA[i, j] = 0;
                }
            }
        }

        // Converter para double[,] para cálculo
        double[,] a = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                a[i, j] = model.MatrizA[i, j];

        // Calcular determinante
        double det = DeterminantGaussian(a, n, out bool ok);

        model.Determinante = ok ? det : 0.0;

        return View(model);
    }

    // Método auxiliar privado para o cálculo do determinante
    private double DeterminantGaussian(double[,] mat, int n, out bool ok)
    {
        double[,] a = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                a[i, j] = mat[i, j];

        const double EPS = 1e-12;
        double detSign = 1.0;

        for (int k = 0; k < n; k++)
        {
            int pivotRow = k;
            double maxAbs = Math.Abs(a[k, k]);
            for (int i = k + 1; i < n; i++)
            {
                double absVal = Math.Abs(a[i, k]);
                if (absVal > maxAbs)
                {
                    maxAbs = absVal;
                    pivotRow = i;
                }
            }

            if (maxAbs < EPS)
            {
                ok = true;
                return 0.0;
            }

            if (pivotRow != k)
            {
                for (int j = k; j < n; j++)
                {
                    double tmp = a[k, j];
                    a[k, j] = a[pivotRow, j];
                    a[pivotRow, j] = tmp;
                }
                detSign = -detSign;
            }

            for (int i = k + 1; i < n; i++)
            {
                double factor = a[i, k] / a[k, k];
                for (int j = k; j < n; j++)
                {
                    a[i, j] -= factor * a[k, j];
                }
            }
        }

        double prod = detSign;
        for (int i = 0; i < n; i++)
            prod *= a[i, i];

        ok = true;
        return prod;
    }

    [HttpGet]
    public IActionResult Inversa()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult Inversa(MatrizViewModel model, string? operacao, string? import, IFormFile? importFile)
    {
        if (model == null)
            model = new MatrizViewModel();

        // Se Colunas não foi submetido (view envia só Linhas), assume quadrado
        if (model.Colunas == 0 && model.Linhas > 0)
            model.Colunas = model.Linhas;

        // limpar validação automática para campos tratados manualmente
        ModelState.Remove(nameof(model.Linhas));
        ModelState.Remove(nameof(model.Colunas));
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.MatrizB));
        ModelState.Remove(nameof(model.Resultado));
        ModelState.Remove(nameof(model.Operacao));
        ModelState.Remove(nameof(model.Escalar));

        // ------------------------
        // 1) IMPORT (se pedido)
        // ------------------------
        if (!string.IsNullOrEmpty(import) && importFile != null)
        {
            if (importFile.Length == 0)
            {
                ModelState.AddModelError("", "Ficheiro vazio.");
                return View(model);
            }

            try
            {
                using var ms = new MemoryStream();
                importFile.CopyTo(ms);
                var bytes = ms.ToArray();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var exportModel = JsonSerializer.Deserialize<MatrixExportModel>(bytes, options);

                if (exportModel == null || exportModel.Data == null)
                {
                    ModelState.AddModelError("", "Formato JSON inválido.");
                    return View(model);
                }

                int rows = exportModel.Rows;
                int cols = exportModel.Columns;

                if (rows < 1 || cols < 1 || rows > 30 || cols > 30)
                {
                    ModelState.AddModelError("", "Dimensões no ficheiro fora do permitido (1..30).");
                    return View(model);
                }

                if (rows != cols)
                {
                    ModelState.AddModelError("", "A matriz tem de ser quadrada para calcular a inversa.");
                    return View(model);
                }

                // validar shape
                if (exportModel.Data.Length != rows)
                {
                    ModelState.AddModelError("", "O número de linhas do 'Data' não corresponde a 'Rows'.");
                    return View(model);
                }
                for (int r = 0; r < rows; r++)
                {
                    if (exportModel.Data[r] == null || exportModel.Data[r].Length != cols)
                    {
                        ModelState.AddModelError("", $"A linha {r} do 'Data' não tem {cols} colunas.");
                        return View(model);
                    }
                }

                model.Linhas = rows;
                model.Colunas = cols;

                if (string.Equals(import, "MatrizA", StringComparison.OrdinalIgnoreCase))
                {
                    model.MatrizA = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizA[i, j] = Convert.ToInt32(Math.Round(exportModel.Data[i][j]));
                }

                return View(model);
            }
            catch (JsonException)
            {
                ModelState.AddModelError("", "Erro ao desserializar o ficheiro JSON.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro a processar o ficheiro: " + ex.Message);
                return View(model);
            }
        }

        // Detectar pedido de exportação (botão name="export")
        if (Request.Form.ContainsKey("export"))
        {
            var which = Request.Form["export"].ToString();

            // Exportar MatrizA
            if (which == "MatrizA")
            {
                var matA = ParseIntMatrixFromForm("MatrizA", model.Linhas, model.Colunas);
                var exportModel = BuildExportModelFromInt(matA, "MatrizA");
                return ExportModelAsJson(exportModel);
            }


        }


        // ------------------------
        // 3) Se chegou aqui: cálculo da inversa solicitado
        // ------------------------
        if (string.IsNullOrEmpty(operacao))
        {
            return View(model);
        }

        if (!string.Equals(operacao, "inversa", StringComparison.OrdinalIgnoreCase))
        {
            return View(model);
        }

        // Validar dimensões
        if (model.Linhas < 1 || model.Colunas < 1 ||
            model.Linhas > 30 || model.Colunas > 30)
        {
            ModelState.AddModelError("", "Dimensões inválidas (1 a 30).");
            return View(model);
        }

        if (model.Linhas != model.Colunas)
        {
            ModelState.AddModelError("", "A matriz tem de ser quadrada para calcular a inversa.");
            return View(model);
        }

        int n = model.Linhas;
        model.Operacao = operacao;

        // Inicializar e preencher MatrizA a partir do form (vazios => 0)
        model.MatrizA = new int[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                string keyA = $"MatrizA[{i},{j}]";
                if (Request.Form.ContainsKey(keyA) && int.TryParse(Request.Form[keyA], out int valA))
                    model.MatrizA[i, j] = valA;
                else
                    model.MatrizA[i, j] = 0;
            }
        }

        // Converter para double[,] e calcular inversa por eliminação Gaussiana
        double[,] a = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                a[i, j] = model.MatrizA[i, j];

        double[,] aug = new double[n, 2 * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                aug[i, j] = a[i, j];
            for (int j = 0; j < n; j++)
                aug[i, n + j] = (i == j) ? 1.0 : 0.0;
        }

        const double EPS = 1e-12;
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            double maxAbs = Math.Abs(aug[col, col]);
            for (int r = col + 1; r < n; r++)
            {
                double aVal = Math.Abs(aug[r, col]);
                if (aVal > maxAbs)
                {
                    maxAbs = aVal;
                    pivot = r;
                }
            }

            if (maxAbs < EPS)
            {
                model.Inversa = null;
                ModelState.AddModelError("", "A matriz é singular (não tem inversa)");
                return View(model);
            }

            if (pivot != col)
            {
                for (int c = 0; c < 2 * n; c++)
                {
                    double tmp = aug[col, c];
                    aug[col, c] = aug[pivot, c];
                    aug[pivot, c] = tmp;
                }
            }

            double pivotVal = aug[col, col];
            for (int c = 0; c < 2 * n; c++)
                aug[col, c] /= pivotVal;

            for (int r = 0; r < n; r++)
            {
                if (r == col) continue;
                double factor = aug[r, col];
                if (Math.Abs(factor) < EPS) continue;
                for (int c = 0; c < 2 * n; c++)
                    aug[r, c] -= factor * aug[col, c];
            }
        }

        double[,] inv = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                inv[i, j] = Math.Round(aug[i, n + j], 2, MidpointRounding.AwayFromZero);

        model.Inversa = inv;
        return View(model);
    }


    [HttpGet]
    public IActionResult Transposta()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult Transposta(MatrizViewModel model, string? operacao, string? import, IFormFile? importFile)
    {
        if (model == null)
            model = new MatrizViewModel();

        // 1️⃣ limpar ModelState
        ModelState.Remove(nameof(model.Linhas));
        ModelState.Remove(nameof(model.Colunas));
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.MatrizB));
        ModelState.Remove(nameof(model.Resultado));
        ModelState.Remove(nameof(model.Operacao));
        ModelState.Remove(nameof(model.Escalar));

        // --- 1) IMPORT server-side: se o botão "import" foi clicado e existe um ficheiro ---
        if (!string.IsNullOrEmpty(import) && importFile != null)
        {
            // validar tamanho / tipo básico (opcional)
            if (importFile.Length == 0)
            {
                ModelState.AddModelError("", "Ficheiro vazio.");
                return View(model);
            }

            try
            {
                using var ms = new MemoryStream();
                importFile.CopyTo(ms);
                var bytes = ms.ToArray();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var exportModel = JsonSerializer.Deserialize<MatrixExportModel>(bytes, options);

                if (exportModel == null || exportModel.Data == null)
                {
                    ModelState.AddModelError("", "Formato JSON inválido.");
                    return View(model);
                }

                int rows = exportModel.Rows;
                int cols = exportModel.Columns;

                if (rows < 1 || cols < 1 || rows > 30 || cols > 30)
                {
                    ModelState.AddModelError("", "Dimensões no ficheiro fora do permitido (1..30).");
                    return View(model);
                }

                // validar shape
                if (exportModel.Data.Length != rows)
                {
                    ModelState.AddModelError("", "O número de linhas do 'Data' não corresponde a 'Rows'.");
                    return View(model);
                }
                for (int r = 0; r < rows; r++)
                {
                    if (exportModel.Data[r] == null || exportModel.Data[r].Length != cols)
                    {
                        ModelState.AddModelError("", $"A linha {r} do 'Data' não tem {cols} colunas.");
                        return View(model);
                    }
                }

                model.Linhas = rows;
                model.Colunas = cols;

                if (import == "MatrizA")
                {
                    model.MatrizA = new int[rows, cols];
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            model.MatrizA[i, j] = Convert.ToInt32(Math.Round(exportModel.Data[i][j]));
                }

                // devolve view com grelha preenchida
                return View(model);
            }
            catch (JsonException)
            {
                ModelState.AddModelError("", "Erro ao desserializar o ficheiro JSON.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro a processar o ficheiro: " + ex.Message);
                return View(model);
            }
        }

        // Detectar pedido de exportação (botão name="export")
        if (Request.Form.ContainsKey("export"))
        {
            var which = Request.Form["export"].ToString();

            // Exportar MatrizA
            if (which == "MatrizA")
            {
                var matA = ParseIntMatrixFromForm("MatrizA", model.Linhas, model.Colunas);
                var exportModel = BuildExportModelFromInt(matA, "MatrizA");
                return ExportModelAsJson(exportModel);
            }


        }

        // Se Colunas não veio (por segurança)
        if (model.Colunas == 0 && model.Linhas > 0)
            model.Colunas = model.Linhas;


        // Criar matriz apenas
        if (string.IsNullOrEmpty(operacao))
        {
            return View(model);
        }

        // Validar dimensões
        if (model.Linhas < 1 || model.Colunas < 1 ||
            model.Linhas > 30 || model.Colunas > 30)
        {
            ModelState.AddModelError("", "Dimensões inválidas (1 a 30).");
            return View(model);
        }
        

        if (!string.Equals(operacao, "transposta", StringComparison.OrdinalIgnoreCase))
        {
            return View(model);
        }

        int linhas = model.Linhas;
        int colunas = model.Colunas;
        model.Operacao = operacao;

        model.MatrizA = new int[linhas, colunas];
        model.Resultado = new int[colunas, linhas]; // dimensões invertidas

        // Ler Matriz A
        for (int i = 0; i < linhas; i++)
        {
            for (int j = 0; j < colunas; j++)
            {
                string key = $"MatrizA[{i},{j}]";
                if (Request.Form.ContainsKey(key) &&
                    int.TryParse(Request.Form[key], out int val))
                    model.MatrizA[i, j] = val;
                else
                    model.MatrizA[i, j] = 0;
            }
        }

        // Calcular transposta
        for (int i = 0; i < linhas; i++)
        {
            for (int j = 0; j < colunas; j++)
            {
                model.Resultado[j, i] = model.MatrizA[i, j];
            }
        }

        return View(model);
    }


    // Tenta desserializar o XML para MatrixExportModel. Retorna true se OK.
    private bool TryDeserializeExportModelFromXml(byte[] bytes, out MatrixExportModel model)
    {
        model = null;

        try
        {
            using var ms = new MemoryStream(bytes);
            var serializer = new XmlSerializer(typeof(MatrixExportModelXml));
            var xmlModel = (MatrixExportModelXml)serializer.Deserialize(ms)!;

            if (xmlModel.Rows <= 0 || xmlModel.Columns <= 0)
                return false;

            if (xmlModel.Data.Count != xmlModel.Rows)
                return false;

            var data = new double[xmlModel.Rows][];

            for (int i = 0; i < xmlModel.Rows; i++)
            {
                if (xmlModel.Data[i].Values.Count != xmlModel.Columns)
                    return false;

                data[i] = xmlModel.Data[i].Values.ToArray();
            }

            model = new MatrixExportModel
            {
                Rows = xmlModel.Rows,
                Columns = xmlModel.Columns,
                Name = xmlModel.Name,
                Data = data
            };

            return true;
        }
        catch
        {
            return false;
        }
    }


    // Gera um FileContentResult com XML representando MatrixExportModel
    private FileContentResult ExportModelAsXml(MatrixExportModel model, string filename)
    {
        var doc = new XDocument(
            new XElement("MatrixExportModel",
                new XElement("Rows", model.Rows),
                new XElement("Columns", model.Columns),
                new XElement("Data",
                    model.Data.Select(r =>
                        new XElement("Row", r.Select(v => new XElement("Value", v)))
                    )
                )
            )
        );

        var xmlString = doc.Declaration == null ? doc.ToString() : doc.Declaration + Environment.NewLine + doc.ToString();
        var bytes = Encoding.UTF8.GetBytes(xmlString);
        return File(bytes, "application/xml", filename);
    }

    private int InferRowsFromFormKeys(string prefix)
    {
        int maxR = -1;
        var pattern = new System.Text.RegularExpressions.Regex($@"^{prefix}\[(\d+),(\d+)\]");
        foreach (var key in Request.Form.Keys)
        {
            var m = pattern.Match(key);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int r))
            {
                if (r > maxR) maxR = r;
            }
        }
        return maxR >= 0 ? maxR + 1 : 0;
    }

    private int InferColsFromFormKeys(string prefix)
    {
        int maxC = -1;
        var pattern = new System.Text.RegularExpressions.Regex($@"^{prefix}\[(\d+),(\d+)\]");
        foreach (var key in Request.Form.Keys)
        {
            var m = pattern.Match(key);
            if (m.Success && int.TryParse(m.Groups[2].Value, out int c))
            {
                if (c > maxC) maxC = c;
            }
        }
        return maxC >= 0 ? maxC + 1 : 0;
    }
    private MatrixExportModel BuildExportModelFromInt(int[,] mat, string name)
    {
        int rows = mat.GetLength(0);
        int cols = mat.GetLength(1);

        var data = new double[rows][];
        for (int i = 0; i < rows; i++)
        {
            data[i] = new double[cols];
            for (int j = 0; j < cols; j++)
                data[i][j] = mat[i, j];
        }

        return new MatrixExportModel
        {
            Name = name,
            Rows = rows,
            Columns = cols,
            Data = data
        };
    }

    // Converte double[,] para MatrixExportModel
    private MatrixExportModel BuildExportModelFromDouble(double[,] mat, string name)
    {
        int rows = mat.GetLength(0);
        int cols = mat.GetLength(1);

        var data = new double[rows][];
        for (int i = 0; i < rows; i++)
        {
            data[i] = new double[cols];
            for (int j = 0; j < cols; j++)
                data[i][j] = mat[i, j];
        }

        return new MatrixExportModel
        {
            Name = name,
            Rows = rows,
            Columns = cols,
            Data = data
        };
    }

    // Serializa e devolve ficheiro .json
    private FileContentResult ExportModelAsJson(MatrixExportModel model)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(model, options);
        var bytes = Encoding.UTF8.GetBytes(json);
        string fileName = $"{(string.IsNullOrEmpty(model.Name) ? "matrix" : model.Name)}_{model.Rows}x{model.Columns}.json";
        return File(bytes, "application/json", fileName);
    }

    // Lê uma matriz de inteiros do Request.Form com nome de prefixo "MatrizA" ou "MatrizB"
    private int[,] ParseIntMatrixFromForm(string prefix, int rows, int cols)
    {
        var mat = new int[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                string key = $"{prefix}[{i},{j}]";
                if (Request.Form.ContainsKey(key) && int.TryParse(Request.Form[key], out int val))
                    mat[i, j] = val;
                else
                    mat[i, j] = 0;
            }
        }
        return mat;
    }

}



