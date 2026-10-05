const http = require("http");
const fs = require("fs");
const path = require("path");

const root = __dirname;
const types = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
};

http.createServer((req, res) => {
  const pedido = decodeURIComponent(req.url.split("?")[0]);
  const relativo = pedido === "/" ? "/renderer/index.html" : pedido;
  const arquivo = path.normalize(path.join(root, relativo));
  if (!arquivo.startsWith(root)) {
    res.writeHead(403);
    res.end();
    return;
  }
  fs.readFile(arquivo, (erro, dados) => {
    if (erro) {
      res.writeHead(404);
      res.end("nao encontrado");
      return;
    }
    res.writeHead(200, { "Content-Type": types[path.extname(arquivo)] || "application/octet-stream" });
    res.end(dados);
  });
}).listen(8765, "127.0.0.1");
