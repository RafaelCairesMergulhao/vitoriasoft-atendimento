const { app, BrowserWindow, ipcMain, dialog, shell, clipboard } = require("electron");
const fs = require("fs");
const os = require("os");
const path = require("path");
const { dadosDePlanilha } = require("../parser");

const arquivoDados = () => path.join(app.getPath("userData"), "dados.json");

function lerJson(caminho) {
  return JSON.parse(fs.readFileSync(caminho, "utf8"));
}

function carregar() {
  const destino = arquivoDados();
  if (fs.existsSync(destino)) return lerJson(destino);
  const semente = path.join(__dirname, "..", "seed", "dados.json");
  const dados = lerJson(semente);
  fs.mkdirSync(path.dirname(destino), { recursive: true });
  fs.writeFileSync(destino, JSON.stringify(dados));
  return dados;
}

function salvar(dados) {
  const destino = arquivoDados();
  const temporario = destino + ".tmp";
  fs.mkdirSync(path.dirname(destino), { recursive: true });
  fs.writeFileSync(temporario, JSON.stringify(dados));
  if (fs.existsSync(destino)) fs.copyFileSync(destino, destino + ".bak");
  fs.renameSync(temporario, destino);
  return true;
}

function mesclar(atual, importado) {
  const ids = new Set(atual.chamados.map((c) => c.id));
  let novos = 0;
  for (const chamado of importado.chamados) {
    if (ids.has(chamado.id)) continue;
    atual.chamados.push(chamado);
    ids.add(chamado.id);
    novos += 1;
  }
  const tecnicos = new Set(atual.config.tecnicos.map((t) => t.toLocaleUpperCase("pt-BR")));
  for (const tecnico of importado.config.tecnicos || []) {
    if (!tecnicos.has(tecnico.toLocaleUpperCase("pt-BR"))) atual.config.tecnicos.push(tecnico);
  }
  return novos;
}

function criarJanela() {
  const janela = new BrowserWindow({
    width: 1380,
    height: 880,
    minWidth: 1100,
    minHeight: 700,
    title: "Vitória Soft Atendimento",
    backgroundColor: "#f3efe4",
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });
  janela.loadFile(path.join(__dirname, "..", "renderer", "index.html"));
}

app.whenReady().then(() => {
  ipcMain.handle("carregar", () => carregar());
  ipcMain.handle("salvar", (_evento, dados) => salvar(dados));
  ipcMain.handle("copiar", (_evento, texto) => {
    clipboard.writeText(String(texto || ""));
    return true;
  });
  ipcMain.handle("abrir", (_evento, url) => {
    const endereco = String(url || "");
    if (/^(https:|tel:|mailto:)/i.test(endereco)) return shell.openExternal(endereco);
    return false;
  });
  ipcMain.handle("exportar", async (_evento, nome, conteudo) => {
    const resposta = await dialog.showSaveDialog({
      defaultPath: nome,
      filters: [
        { name: "Planilha CSV", extensions: ["csv"] },
        { name: "Todos", extensions: ["*"] },
      ],
    });
    if (resposta.canceled || !resposta.filePath) return false;
    fs.writeFileSync(resposta.filePath, conteudo, "utf8");
    return true;
  });
  ipcMain.handle("importar", async () => {
    const resposta = await dialog.showOpenDialog({
      filters: [{ name: "Planilha Excel", extensions: ["xlsx"] }],
      properties: ["openFile"],
    });
    if (resposta.canceled || !resposta.filePaths[0]) return { cancelado: true };
    const AdmZip = require("adm-zip");
    const pasta = fs.mkdtempSync(path.join(os.tmpdir(), "vs-planilha-"));
    new AdmZip(resposta.filePaths[0]).extractAllTo(pasta, true);
    const importado = dadosDePlanilha(path.join(pasta, "xl"));
    const atual = carregar();
    const novos = mesclar(atual, importado);
    salvar(atual);
    return { novos, total: atual.chamados.length, dados: atual };
  });
  criarJanela();
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
