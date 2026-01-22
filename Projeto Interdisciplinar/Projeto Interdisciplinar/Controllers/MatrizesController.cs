using Microsoft.AspNetCore.Mvc;
using Projeto_Interdisciplinar.Models;
using System;

public class MatrizesController : Controller
{
    [HttpGet]
    public IActionResult SomaSubtracao()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult SomaSubtracao(MatrizViewModel model, string operacao)
    {
        // Segurança: garantir que o model não é nulo
        if (model == null) model = new MatrizViewModel();

        // Validar dimensões
        if (model.Linhas < 1 || model.Colunas < 1 || model.Linhas > 30 || model.Colunas > 30)
        {
            ModelState.AddModelError("", "Dimensões inválidas. Linhas e colunas entre 1 e 30.");
            return View(model);
        }

        // Se o utilizador só clicou em "Criar Matrizes"
        if (string.IsNullOrEmpty(operacao))
        {
            // NÃO inicializar matrizes → inputs ficam vazios
            return View(model);
        }

        int linhas = model.Linhas;
        int colunas = model.Colunas;

        // Inicializar matrizes (sempre inicializamos aqui)
        model.MatrizA = new int[linhas, colunas];
        model.MatrizB = new int[linhas, colunas];
        model.Resultado = new int[linhas, colunas];

        // Preencher MatrizA e MatrizB com os valores do form
        // Assumimos que os inputs na View têm nomes do tipo: name="MatrizA[0,0]" e name="MatrizB[0,0]"
        for (int i = 0; i < linhas; i++)
        {
            for (int j = 0; j < colunas; j++)
            {
                string keyA = $"MatrizA[{i},{j}]";
                string keyB = $"MatrizB[{i},{j}]";

                // Ler e converter com fallback para 0 quando vazio/inválido
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

        // Devolver a view com o model preenchido (mostra as matrizes e o resultado)
        return View(model);
    }

    [HttpGet]
    public IActionResult MultiplicacaoMatriz()
    {
        return View(new MatrizViewModel());
    }

    [HttpPost]
    public IActionResult MultiplicacaoMatriz(MatrizViewModel model, string operacao)
    {
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.MatrizB));
        ModelState.Remove(nameof(model.Resultado));
        ModelState.Remove(nameof(model.Operacao));


        if (model == null)
            model = new MatrizViewModel();


        // Validar dimensões mín/max para ambas as matrizes
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

        // Só executa multiplicação quando o botão "Multiplicar" foi clicado
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

        // Inicializar matrizes para leitura e resultado
        model.MatrizA = new int[linhasA, colsA];
        model.MatrizB = new int[linhasB, colsB];
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
    public IActionResult MultiplicacaoEscalar(MatrizViewModel model, string operacao)
    {

        if (model == null)
            model = new MatrizViewModel();

        if (model.Linhas < 1 || model.Colunas < 1 ||
            model.Linhas > 30 || model.Colunas > 30)
        {
            ModelState.AddModelError("", "Dimensões inválidas (1 a 30).");
            return View(model);
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
    public IActionResult Determinante(MatrizViewModel model, string operacao)
    {
        if (model == null)
            model = new MatrizViewModel();

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
    public IActionResult Inversa(MatrizViewModel model, string operacao)
    {
        if (model == null)
            model = new MatrizViewModel();

        // Se Colunas não foi submetido (view envia só Linhas), assume matriz quadrada
        if (model.Colunas == 0 && model.Linhas > 0)
            model.Colunas = model.Linhas;

        // Limpar validação automática para campos tratados manualmente
        ModelState.Remove(nameof(model.MatrizA));
        ModelState.Remove(nameof(model.Inversa));
        ModelState.Remove(nameof(model.Operacao));

        // Se o utilizador só pediu "Criar Matriz" (operacao vazio), devolve a grelha vazia
        if (string.IsNullOrEmpty(operacao))
        {
            return View(model);
        }

        // Só processar quando o botão "inversa" foi clicado
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

        // Só faz sentido para matriz quadrada
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

        // Converter para double[,] para cálculo
        double[,] a = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                a[i, j] = model.MatrizA[i, j];

        // Calcular inversa por eliminação Gaussiana (retorna null se singular)
        bool ok = InverseGaussian(a, n, out double[,]? inv);

        if (!ok || inv == null)
        {
            model.Inversa = null;
            ModelState.AddModelError("", "A matriz é singular (não tem inversa) ou ocorreu erro numérico.");
            return View(model);
        }

        model.Inversa = inv;
        return View(model);
    }


    // Método auxiliar: devolve true se conseguiu calcular a inversa e coloca resultado em 'inv'
    private bool InverseGaussian(double[,] mat, int n, out double[,]? inv)
    {
        // Montar matriz aumentada [A | I]
        double[,] aug = new double[n, 2 * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                aug[i, j] = mat[i, j];

            for (int j = 0; j < n; j++)
                aug[i, n + j] = (i == j) ? 1.0 : 0.0;
        }

        const double EPS = 1e-12;

        // Eliminação com pivot parcial
        for (int col = 0; col < n; col++)
        {
            // pivot: linha com maior |aug[row,col]|
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
                inv = null;
                return false; // singular ou numericamente instável
            }

            // trocar linhas se necessário
            if (pivot != col)
            {
                for (int c = 0; c < 2 * n; c++)
                {
                    double tmp = aug[col, c];
                    aug[col, c] = aug[pivot, c];
                    aug[pivot, c] = tmp;
                }
            }

            // normalizar linha do pivot
            double pivotVal = aug[col, col];
            for (int c = 0; c < 2 * n; c++)
                aug[col, c] /= pivotVal;

            // eliminar outras linhas
            for (int r = 0; r < n; r++)
            {
                if (r == col) continue;
                double factor = aug[r, col];
                if (Math.Abs(factor) < EPS) continue;
                for (int c = 0; c < 2 * n; c++)
                    aug[r, c] -= factor * aug[col, c];
            }
        }

        // extrair inversa da matriz aumentada
        inv = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                inv[i, j] = aug[i, n + j];

        return true;
    }

}



