const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("api", {
  carregar: () => ipcRenderer.invoke("carregar"),
  salvar: (dados) => ipcRenderer.invoke("salvar", dados),
  copiar: (texto) => ipcRenderer.invoke("copiar", texto),
  abrir: (url) => ipcRenderer.invoke("abrir", url),
  exportar: (nome, conteudo) => ipcRenderer.invoke("exportar", nome, conteudo),
  importar: () => ipcRenderer.invoke("importar"),
});
