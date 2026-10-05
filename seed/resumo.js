const dados = require("./dados.json");
const meses = {};
for (const c of dados.chamados) {
  const k = (c.data || "sem-data").slice(0, 7);
  meses[k] = (meses[k] || 0) + 1;
}
console.log("ramais", JSON.stringify(dados.ramais, null, 2));
console.log("dicas", dados.dicasTelefone);
console.log("status", dados.chamados.reduce((a, c) => (a[c.status] = (a[c.status] || 0) + 1, a), {}));
console.log("meses");
for (const k of Object.keys(meses).sort()) console.log(k, meses[k]);
