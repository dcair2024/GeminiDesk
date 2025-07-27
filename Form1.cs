using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json; 
using System.Text.Json;
using Microsoft.Extensions.Configuration; 
using System.IO; // Para File.WriteAllText

namespace GeminiDesk
{
    public partial class Form1 : Form
    {
        private readonly string chaveApi;
        private bool modoDebug = true; // Mantido para compatibilidade, mas não usado diretamente no fluxo principal

        public Form1()
        {
            InitializeComponent();
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            IConfigurationRoot configuration = builder.Build();
            chaveApi = configuration.GetSection("AppSettings:ChaveApi").Value;


            // Configurações visuais do campo histórico
            txtHistorico.Font = new Font("Consolas", 10);
            txtHistorico.ReadOnly = true;
            txtHistorico.ScrollBars = ScrollBars.Vertical;
            txtHistorico.ForeColor = Color.White;
            txtHistorico.BackColor = Color.Black;

            
        }

       


        private async void btn_Enviar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPergunta.Text))
            {
                MessageBox.Show("Digite uma pergunta antes de enviar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btn_Enviar.Enabled = false; // Desabilita o botão para evitar envios múltiplos
            string pergunta = txtPergunta.Text.Trim();
            txtPergunta.Clear(); // Limpa a caixa de texto da pergunta

            
            AdicionarAoHistorico($"Você: {pergunta}");

           
            txtHistorico.AppendText("IA: Pensando...\r\n\r\n");
            txtHistorico.SelectionStart = txtHistorico.Text.Length;
            txtHistorico.ScrollToCaret();


            try
            {
                string tipoResposta = chkDetalhado.Checked 
                    ? "Responda com o máximo de detalhes possíveis."
                    : "Responda de forma curta e objetiva.";

                string perguntaParaIA = tipoResposta + Environment.NewLine + pergunta;

                string respostaDaIA = await EnviarPerguntaParaIA(perguntaParaIA);

               
                AdicionarAoHistorico($"IA: {respostaDaIA}");

                // Posiciona no fim do texto para o scroll seguir normalmente
                txtHistorico.SelectionStart = txtHistorico.Text.Length;
                txtHistorico.ScrollToCaret();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btn_Enviar.Enabled = true; // Reabilita o botão
            }
        }

        private void btnLimpar_Click(object sender, EventArgs e)
        {
            txtHistorico.Clear();
        }

        /// <summary>
        /// Adiciona texto ao histórico com um timestamp e força o scroll automático.
        /// </summary>
        /// <param name="texto">O texto a ser adicionado (ex: "Você: Olá", "IA: Oi")</param>
        private void AdicionarAoHistorico(string texto)
        {
            // Captura a hora atual no formato [HH:mm]
            string hora = DateTime.Now.ToString("[HH:mm]");
            // Adiciona a hora e o texto, seguido de duas quebras de linha para espaçamento
            txtHistorico.AppendText($"{hora} {texto}{Environment.NewLine}{Environment.NewLine}");
            // Move o cursor para o final do texto para garantir o scroll automático
            txtHistorico.SelectionStart = txtHistorico.Text.Length;
            txtHistorico.ScrollToCaret();
        }


        private async Task<string> EnviarPerguntaParaIA(string pergunta)
        {
            using var client = new HttpClient();
            // A API Key deve ser tratada com segurança, idealmente não hardcoded ou exposta em clientes.
            // Para este exemplo, estamos usando a que você já tem do appsettings.json.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chaveApi);
            client.DefaultRequestHeaders.Add("HTTP-Referer", "https://openrouter.ai"); // Necessário para OpenRouter
            client.DefaultRequestHeaders.Add("User-Agent", "GeminiDesk-CSharp"); // Identifica sua aplicação

            var corpo = new
            {
                model = "mistralai/mistral-7b-instruct", // Modelo de IA a ser usado
                messages = new[] {
                    new { role = "user", content = pergunta } // A pergunta do usuário
                },
                temperature = 0.9, // Criatividade da resposta (0.0 a 1.0)
                max_tokens = 300 // Limite de tokens na resposta
            };

            var json = JsonConvert.SerializeObject(corpo); // Serializa o objeto para JSON
            var content = new StringContent(json, Encoding.UTF8, "application/json"); // Cria o conteúdo da requisição

            var resposta = await client.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);
            resposta.EnsureSuccessStatusCode(); // Lança uma exceção se o status não for de sucesso (2xx)

            var respostaJson = await resposta.Content.ReadAsStringAsync(); // Lê a resposta como string JSON

            // Debug opcional: imprime a resposta JSON completa no console
            Console.WriteLine("DEBUG >>>\n" + respostaJson + "\n<<<");

            try
            {
                // Analisa a resposta JSON para extrair o conteúdo da mensagem da IA
                using JsonDocument doc = JsonDocument.Parse(respostaJson);

                if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out JsonElement message) &&
                    message.TryGetProperty("content", out JsonElement contentElement))
                {
                    return contentElement.GetString() ?? "Resposta vazia."; // Retorna o texto da resposta
                }
                else
                {
                    // Caso a estrutura da resposta seja inesperada
                    return "Erro: resposta da IA está vazia ou inválida.\nResposta bruta:\n" + respostaJson;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Erro ao analisar o JSON
                return "Erro: resposta mal formatada ou inválida.\nResposta bruta:\n" + respostaJson;
            }
        }

        private void btnCopiarResposta_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtHistorico.Text))
            {
                Clipboard.SetText(txtHistorico.Text);
                MessageBox.Show("Histórico copiado para a área de transferência.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("O histórico está vazio para copiar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnSalvaHistorico_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtHistorico.Text))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Arquivos de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*",
                    Title = "Salvar Histórico",
                    FileName = "historico.txt"
                };
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(saveFileDialog.FileName, txtHistorico.Text);
                        MessageBox.Show("Histórico salvo com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erro ao salvar o histórico: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("O histórico está vazio para salvar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
