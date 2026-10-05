let db = null;
let cacheFarmacias = null;
const ui = {
  tela: "painel",
  busca: "",
  filtroCanal: "",
  farmaciaAberta: "",
  buscaCliente: "",
  filtroStatus: "abertos",
  filtroTecnico: "",
  pagina: 0,
  rascunho: null,
  cliente: "",
  toast: "",
};

const acoes = {
  tela: (d) => {
    ui.tela = d.tela;
    ui.pagina = 0;
    if (d.tela === "clientes") ui.farmaciaAberta = "";
    if (d.tela === "ficha" && !ui.rascunho) novoChamado();
    else render();
  },
  novo: () => novoChamado(),
  abrir: (d) => editar(d.id),
  pagina: (d) => {
    ui.pagina = Math.max(0, ui.pagina + Number(d.delta));
    render();
  },
  canal: (d) => {
    ui.rascunho.canal = d.id;
    render();
  },
  status: (d) => {
    ui.rascunho.status = d.id;
    if (d.id === "concluido" && !ui.rascunho.horaFim) ui.rascunho.horaFim = horaAgora();
    render();
  },
  salvar: () => salvarChamado(false),
  salvarNovo: () => salvarChamado(true),
  excluir: () => excluirChamado(),
  frase: (d) => {
    const frase = db.config.frases[Number(d.i)] || "";
    ui.rascunho.obs = ui.rascunho.obs ? ui.rascunho.obs + "\n" + frase : frase;
    render();
  },
  copiar: async (d) => {
    const texto = decodeURIComponent(d.texto || "");
    if (window.api) await window.api.copiar(texto);
    else await navigator.clipboard.writeText(texto);
    avisar("Texto copiado.");
  },
  ligar: (d) => abrir("tel:+55" + d.num),
  whats: (d) => abrir("https://wa.me/55" + d.num),
  cliente: (d) => {
    ui.farmaciaAberta = d.nome;
    ui.tela = "clientes";
    render();
  },
  exportar: () => exportar(),
  importar: () => importarPlanilha(),
  addTecnico: () => acrescentarLista("tecnicos", "novo-tecnico"),
  delTecnico: (d) => removerLista("tecnicos", Number(d.i)),
  addFrase: () => acrescentarLista("frases", "nova-frase"),
  delFrase: (d) => removerLista("frases", Number(d.i)),
  addCanal: () => acrescentarCanal(),
  delCanal: (d) => {
    if (db.config.canais.length < 2) return;
    db.config.canais.splice(Number(d.i), 1);
    persistir();
    render();
  },
  addCampo: () => acrescentarCampo(),
  delCampo: (d) => {
    db.config.camposExtras.splice(Number(d.i), 1);
    persistir();
    render();
  },
  addRamal: () => acrescentarRamal(),
  delRamal: (d) => {
    db.ramais.splice(Number(d.i), 1);
    persistir();
    render();
  },
};

function esc(valor) {
  return String(valor ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

function hojeISO() {
  const d = new Date();
  return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0");
}

function horaAgora() {
  const d = new Date();
  return String(d.getHours()).padStart(2, "0") + ":" + String(d.getMinutes()).padStart(2, "0");
}

function dataBr(iso) {
  if (!iso) return "";
  const [a, m, d] = String(iso).slice(0, 10).split("-");
  return d && m && a ? d + "/" + m + "/" + a : iso;
}

function digitos(valor) {
  return String(valor || "").replace(/\D/g, "");
}

function fone(ddd, numero) {
  const n = digitos(numero);
  if (!n) return "";
  const bonito = n.length === 9 ? n.slice(0, 5) + "-" + n.slice(5) : n.length === 8 ? n.slice(0, 4) + "-" + n.slice(4) : n;
  return digitos(ddd) ? "(" + digitos(ddd) + ") " + bonito : bonito;
}

function canalDe(id) {
  return db.config.canais.find((c) => c.id === id) || { id: "", nome: "Não informado", cor: "#8a8175" };
}

function statusDe(id) {
  return db.config.status.find((s) => s.id === id) || { id, nome: id || "—", cor: "#8a8175" };
}

function etiqueta(item) {
  return '<span class="etiqueta" style="color:' + esc(item.cor) + '"><i class="ponto"></i>' + esc(item.nome) + "</span>";
}

function farmacias() {
  if (cacheFarmacias) return cacheFarmacias;
  const mapa = new Map();
  for (const c of db.chamados) {
    const nome = (c.farmacia || "").trim();
    if (!nome) continue;
    const chave = nome.toLocaleUpperCase("pt-BR");
    const atual = mapa.get(chave) || { nome, qtd: 0, ddd: "", telefone: "", telefone2: "", codigo: "", ultima: "" };
    atual.qtd += 1;
    if (c.telefone) {
      atual.ddd = c.ddd || atual.ddd;
      atual.telefone = c.telefone;
    }
    if (c.telefone2) atual.telefone2 = c.telefone2;
    if (c.codigo) atual.codigo = c.codigo;
    if ((c.data || "") > atual.ultima) atual.ultima = c.data || "";
    mapa.set(chave, atual);
  }
  cacheFarmacias = [...mapa.values()].sort((a, b) => b.qtd - a.qtd);
  return cacheFarmacias;
}

function acharFarmacia(nome) {
  const chave = String(nome || "").trim().toLocaleUpperCase("pt-BR");
  return farmacias().find((f) => f.nome.toLocaleUpperCase("pt-BR") === chave);
}

function historicoFarmacia(nome) {
  const chave = String(nome || "").trim().toLocaleUpperCase("pt-BR");
  return db.chamados.filter((c) => (c.farmacia || "").trim().toLocaleUpperCase("pt-BR") === chave).slice().reverse();
}

function novoProtocolo() {
  let n = db.chamados.length + 1;
  const usados = new Set(db.chamados.map((c) => c.protocolo));
  let protocolo = "AT-" + String(n).padStart(5, "0");
  while (usados.has(protocolo)) {
    n += 1;
    protocolo = "AT-" + String(n).padStart(5, "0");
  }
  return protocolo;
}

function rascunhoVazio() {
  return {
    id: "",
    protocolo: novoProtocolo(),
    canal: "",
    canalInformado: true,
    data: hojeISO(),
    horaInicio: horaAgora(),
    horaFim: "",
    status: "aberto",
    cliente: "",
    farmacia: "",
    codigo: "",
    tecnico: db.config.tecnicoLocal || "",
    assunto: "",
    ddd: "",
    telefone: "",
    telefone2: "",
    obs: "",
    agendamento: "",
    prioridade: "normal",
    marcador: "",
    ticket: "",
    historico: [],
    extras: {},
  };
}

function novoChamado() {
  ui.rascunho = rascunhoVazio();
  ui.tela = "ficha";
  render();
}

function editar(id) {
  const chamado = db.chamados.find((c) => c.id === id);
  if (!chamado) return;
  ui.rascunho = JSON.parse(JSON.stringify(chamado));
  ui.rascunho.extras = ui.rascunho.extras || {};
  ui.tela = "ficha";
  render();
}

function lerRascunhoDoFormulario() {
  const r = ui.rascunho;
  const campo = (id) => {
    const el = document.getElementById(id);
    return el ? el.value : "";
  };
  r.data = campo("f-data");
  r.horaInicio = campo("f-ini");
  r.horaFim = campo("f-fim");
  r.cliente = campo("f-cliente");
  r.farmacia = campo("f-farmacia");
  r.codigo = campo("f-codigo");
  r.tecnico = campo("f-tecnico");
  r.assunto = campo("f-assunto");
  r.ddd = campo("f-ddd");
  r.telefone = campo("f-tel");
  r.telefone2 = campo("f-tel2");
  r.obs = campo("f-obs");
  r.agendamento = campo("f-ag");
  r.prioridade = campo("f-prio");
  r.marcador = document.getElementById("f-marca") && document.getElementById("f-marca").checked ? "X" : "";
  for (const extra of db.config.camposExtras) r.extras[extra.id] = campo("x-" + extra.id);
}

function salvarChamado(abrirOutro) {
  lerRascunhoDoFormulario();
  const r = ui.rascunho;
  if (!r.farmacia.trim() && !r.cliente.trim()) return avisar("Informe a farmácia ou o cliente.");
  if (!r.assunto.trim()) return avisar("Informe o assunto do chamado.");
  if (!r.canal) return avisar("Escolha de onde veio o chamado: ligação, WhatsApp, pedido interno ou e-mail.");
  r.canalInformado = true;
  if (r.tecnico && !db.config.tecnicos.some((t) => t.toLocaleLowerCase("pt-BR") === r.tecnico.toLocaleLowerCase("pt-BR"))) {
    db.config.tecnicos.push(r.tecnico);
  }
  r.atualizadoEm = new Date().toISOString();
  if (!r.criadoEm) r.criadoEm = (r.data || hojeISO()) + "T" + (r.horaInicio || horaAgora());
  if (!r.id) {
    r.id = "novo-" + Date.now();
    db.chamados.push(JSON.parse(JSON.stringify(r)));
  } else {
    const i = db.chamados.findIndex((c) => c.id === r.id);
    if (i >= 0) db.chamados[i] = JSON.parse(JSON.stringify(r));
  }
  cacheFarmacias = null;
  persistir();
  avisar("Chamado " + r.protocolo + " salvo.");
  if (abrirOutro) novoChamado();
  else {
    ui.tela = "fila";
    ui.filtroStatus = "abertos";
    render();
  }
}

function excluirChamado() {
  if (!ui.rascunho.id) {
    ui.tela = "fila";
    return render();
  }
  if (!confirm("Excluir o chamado " + ui.rascunho.protocolo + "?")) return;
  db.chamados = db.chamados.filter((c) => c.id !== ui.rascunho.id);
  cacheFarmacias = null;
  persistir();
  ui.tela = "fila";
  avisar("Chamado excluído.");
  render();
}

function fila() {
  const q = ui.busca.trim().toLocaleLowerCase("pt-BR");
  return db.chamados.filter((c) => {
    if (ui.filtroCanal === "__vazio") { if (c.canal) return false; }
    else if (ui.filtroCanal && c.canal !== ui.filtroCanal) return false;
    if (ui.filtroTecnico && (c.tecnico || "") !== ui.filtroTecnico) return false;
    if (ui.filtroStatus === "abertos" && c.status === "concluido") return false;
    else if (ui.filtroStatus && ui.filtroStatus !== "abertos" && ui.filtroStatus !== "todos" && c.status !== ui.filtroStatus) return false;
    if (!q) return true;
    const texto = [c.protocolo, c.farmacia, c.cliente, c.assunto, c.telefone, c.telefone2, c.obs, c.tecnico, c.codigo].join(" ").toLocaleLowerCase("pt-BR");
    return texto.includes(q);
  }).sort((a, b) => String(b.data || "").localeCompare(String(a.data || "")) || String(b.protocolo).localeCompare(String(a.protocolo)));
}

function htmlTopo(titulo, texto) {
  return '<div class="topo"><div><h1>' + esc(titulo) + "</h1><p>" + esc(texto) + "</p></div></div>";
}

function htmlPainel() {
  const hoje = hojeISO();
  const abertos = db.chamados.filter((c) => c.status === "aberto").length;
  const andamento = db.chamados.filter((c) => c.status === "andamento").length;
  const retorno = db.chamados.filter((c) => c.status === "retorno" || c.status === "agendado").length;
  const deHoje = db.chamados.filter((c) => (c.data || "") === hoje);
  const canais = db.config.canais.map((canal) => {
    const n = deHoje.filter((c) => c.canal === canal.id).length;
    return '<div class="linha-canal"><span>' + etiqueta(canal) + "</span><strong>" + n + "</strong></div>";
  }).join("");
  const recentes = db.chamados.slice().reverse().slice(0, 6).map((c) => (
    '<div class="item-lista"><strong>' + esc(c.farmacia || c.cliente || "Sem nome") + "</strong><span>" +
    esc(dataBr(c.data) + " · " + (c.assunto || "Sem assunto")) + "</span></div>"
  )).join("");
  return htmlTopo("Painel do dia", db.chamados.length + " chamados na base, do histórico da planilha até os novos.") +
    '<div class="cartoes">' +
    cartao("Em aberto", abertos) +
    cartao("Em atendimento", andamento) +
    cartao("Retorno ou agendado", retorno) +
    cartao("Registrados hoje", deHoje.length) +
    "</div>" +
    '<div class="grade"><section class="painel"><h2>Canais de hoje</h2>' + (canais || '<p class="aviso">Nenhum chamado com a data de hoje.</p>') +
    '</section><section class="painel"><h2>Últimos registros</h2>' + recentes + "</section></div>";
}

function cartao(rotulo, valor) {
  return '<article class="cartao"><span>' + esc(rotulo) + "</span><strong>" + esc(valor) + "</strong></article>";
}

function htmlFila() {
  const itens = fila();
  const tamanho = 40;
  const pagina = Math.min(ui.pagina, Math.max(0, Math.ceil(itens.length / tamanho) - 1));
  ui.pagina = pagina;
  const fatia = itens.slice(pagina * tamanho, pagina * tamanho + tamanho);
  const linhas = fatia.map((c) => (
    '<tr data-acao="abrir" data-id="' + esc(c.id) + '"><td>' + esc(c.protocolo) + "<br>" + etiqueta(statusDe(c.status)) +
    "</td><td>" + esc(dataBr(c.data)) + "<br><span>" + esc(c.horaInicio || "") + "</span></td><td><strong>" +
    esc(c.farmacia || "—") + "</strong><br>" + esc(c.cliente || "") + "</td><td>" + esc(c.assunto || "") +
    "</td><td>" + etiqueta(canalDe(c.canal)) + "</td><td>" + esc(c.tecnico || "—") + "<br>" + esc(fone(c.ddd, c.telefone)) + "</td></tr>"
  )).join("");
  return htmlTopo("Fila de chamados", itens.length + " chamados neste filtro.") +
    '<div class="ferramentas">' +
    '<input id="busca" class="busca" placeholder="Buscar farmácia, cliente, telefone, assunto" value="' + esc(ui.busca) + '">' +
    selectHtml("filtro-status", [
      ["abertos", "Em aberto e em andamento"],
      ["todos", "Histórico completo"],
      ...db.config.status.map((s) => [s.id, s.nome]),
    ], ui.filtroStatus) +
    selectHtml("filtro-canal", [["", "Todos os canais"], ...db.config.canais.map((c) => [c.id, c.nome]), ["__vazio", "Não informado"]], ui.filtroCanal) +
    selectHtml("filtro-tecnico", [["", "Todos os técnicos"], ...db.config.tecnicos.map((t) => [t, t])], ui.filtroTecnico) +
    "</div>" +
    '<div class="painel" style="overflow:auto"><table class="tabela"><thead><tr><th>Protocolo</th><th>Data</th><th>Farmácia</th><th>Assunto</th><th>Canal</th><th>Técnico</th></tr></thead><tbody>' +
    (linhas || '<tr><td colspan="6">Nenhum chamado encontrado.</td></tr>') +
    '</tbody></table></div><div class="paginas"><span>Página ' + (pagina + 1) + " de " + Math.max(1, Math.ceil(itens.length / tamanho)) +
    '</span><div><button class="secundario" data-acao="pagina" data-delta="-1">Anterior</button> <button class="secundario" data-acao="pagina" data-delta="1">Próxima</button></div></div>';
}

function selectHtml(id, opcoes, valor) {
  return '<select id="' + id + '">' + opcoes.map(([v, nome]) => (
    '<option value="' + esc(v) + '"' + (String(v) === String(valor) ? " selected" : "") + ">" + esc(nome) + "</option>"
  )).join("") + "</select>";
}

function htmlFicha() {
  const r = ui.rascunho;
  const farm = acharFarmacia(r.farmacia);
  const anteriores = historicoFarmacia(r.farmacia).slice(0, 5);
  const numero = digitos((r.ddd || "") + (r.telefone || ""));
  const canais = db.config.canais.map((c) => (
    '<button type="button" data-acao="canal" data-id="' + esc(c.id) + '" class="' + (r.canal === c.id ? "escolhido" : "") + '">' + esc(c.nome) + "</button>"
  )).join("");
  const status = db.config.status.map((s) => (
    '<button type="button" class="secundario" data-acao="status" data-id="' + esc(s.id) + '"' + (r.status === s.id ? ' style="outline:2px solid ' + esc(s.cor) + '"' : "") + ">" + esc(s.nome) + "</button>"
  )).join("");
  const extras = db.config.camposExtras.map((campo) => {
    const valor = (r.extras && r.extras[campo.id]) || "";
    if (campo.tipo === "lista") {
      const opcoes = String(campo.opcoes || "").split(",").map((o) => o.trim()).filter(Boolean);
      return '<div class="grupo"><label>' + esc(campo.nome) + "</label>" + selectHtml("x-" + campo.id, [["", "Selecione"], ...opcoes.map((o) => [o, o])], valor) + "</div>";
    }
    const tipo = campo.tipo === "data" ? "date" : "text";
    return '<div class="grupo"><label>' + esc(campo.nome) + '</label><input id="x-' + esc(campo.id) + '" type="' + tipo + '" value="' + esc(valor) + '"></div>';
  }).join("");
  const frases = db.config.frases.slice(0, 8).map((frase, i) => (
    '<button type="button" class="atalho" data-acao="frase" data-i="' + i + '">' + esc(frase.slice(0, 90)) + "</button>"
  )).join("");
  const lado = farm
    ? "<h2>" + esc(farm.nome) + "</h2><p>" + esc(farm.qtd + " chamados anteriores") + "</p><p>" + esc(fone(farm.ddd, farm.telefone)) + "</p>" +
      anteriores.map((c) => '<div class="item-lista"><strong>' + esc(dataBr(c.data) + " · " + (c.assunto || "Sem assunto")) + "</strong><span>" + esc(c.tecnico || "") + "</span></div>").join("")
    : "<h2>Histórico</h2><p>Ao digitar a farmácia, os telefones e os chamados anteriores aparecem aqui.</p>";
  return htmlTopo(r.id ? r.protocolo : "Novo chamado", "Registre a origem e o que a farmácia precisa.") +
    '<div class="layout-ficha"><form class="ficha" id="form-chamado">' +
    '<div class="canais">' + canais + "</div>" +
    '<div class="colunas"><div class="grupo"><label>Data</label><input id="f-data" type="date" value="' + esc(r.data) + '"></div>' +
    '<div class="grupo"><label>Hora início</label><input id="f-ini" value="' + esc(r.horaInicio) + '"></div>' +
    '<div class="grupo"><label>Hora fim</label><input id="f-fim" value="' + esc(r.horaFim) + '"></div></div>' +
    '<div class="grupo"><label>Farmácia</label><input id="f-farmacia" list="lista-farmacias" value="' + esc(r.farmacia) + '"></div>' +
    '<datalist id="lista-farmacias">' + farmacias().slice(0, 800).map((f) => '<option value="' + esc(f.nome) + '"></option>').join("") + "</datalist>" +
    '<div class="colunas"><div class="grupo"><label>Cliente</label><input id="f-cliente" value="' + esc(r.cliente) + '"></div>' +
    '<div class="grupo"><label>Código</label><input id="f-codigo" value="' + esc(r.codigo) + '"></div>' +
    '<div class="grupo"><label>Técnico</label><input id="f-tecnico" list="lista-tecnicos" value="' + esc(r.tecnico) + '"></div></div>' +
    '<datalist id="lista-tecnicos">' + db.config.tecnicos.map((t) => '<option value="' + esc(t) + '"></option>').join("") + "</datalist>" +
    '<div class="grupo"><label>Assunto</label><input id="f-assunto" value="' + esc(r.assunto) + '"></div>' +
    '<div class="colunas"><div class="grupo"><label>DDD</label><input id="f-ddd" value="' + esc(r.ddd) + '"></div>' +
    '<div class="grupo"><label>Telefone</label><input id="f-tel" value="' + esc(r.telefone) + '"></div>' +
    '<div class="grupo"><label>Telefone 2</label><input id="f-tel2" value="' + esc(r.telefone2) + '"></div></div>' +
    (numero ? '<div class="acoes"><button type="button" class="secundario" data-acao="ligar" data-num="' + esc(numero) + '">Ligar</button><button type="button" class="secundario" data-acao="whats" data-num="' + esc(numero) + '">Abrir WhatsApp</button></div>' : "") +
    '<div class="grupo"><label>Observações</label><textarea id="f-obs">' + esc(r.obs) + "</textarea></div>" +
    '<div class="colunas"><div class="grupo"><label>Agendamento</label><input id="f-ag" type="datetime-local" value="' + esc(String(r.agendamento || "").slice(0, 16)) + '"></div>' +
    '<div class="grupo"><label>Prioridade</label>' + selectHtml("f-prio", [["normal", "Normal"], ["alta", "Alta"]], r.prioridade) + "</div>" +
    '<div class="grupo"><label>Marcador X</label><input id="f-marca" type="checkbox"' + (r.marcador === "X" ? " checked" : "") + "></div></div>" +
    extras +
    '<div class="acoes">' + status + "</div>" +
    '<div class="acoes"><button type="button" class="primario" data-acao="salvar">Salvar</button><button type="button" class="secundario" data-acao="salvarNovo">Salvar e abrir outro</button>' +
    (r.id ? '<button type="button" class="perigo" data-acao="excluir">Excluir</button>' : "") + "</div></form>" +
    '<aside class="painel">' + lado + '<h2 style="margin-top:16px">Frases prontas</h2><div class="historico">' + frases + "</div></aside></div>";
}

function htmlClientes() {
  if (ui.farmaciaAberta) {
    const lista = historicoFarmacia(ui.farmaciaAberta);
    const linhas = lista.slice(0, 80).map((c) => (
      '<tr data-acao="abrir" data-id="' + esc(c.id) + '"><td>' + esc(dataBr(c.data)) + "</td><td>" + esc(c.assunto || "") +
      "</td><td>" + etiqueta(canalDe(c.canal)) + "</td><td>" + etiqueta(statusDe(c.status)) + "</td><td>" + esc(c.tecnico || "") + "</td></tr>"
    )).join("");
    return htmlTopo(ui.farmaciaAberta, lista.length + " chamados desta farmácia.") +
      '<button class="fantasma" data-acao="tela" data-tela="clientes" id="voltar-clientes">Voltar para a lista</button>' +
      '<div class="painel" style="overflow:auto"><table class="tabela"><thead><tr><th>Data</th><th>Assunto</th><th>Canal</th><th>Status</th><th>Técnico</th></tr></thead><tbody>' +
      linhas + "</tbody></table></div>";
  }
  const q = ui.buscaCliente.trim().toLocaleLowerCase("pt-BR");
  const itens = farmacias().filter((f) => !q || f.nome.toLocaleLowerCase("pt-BR").includes(q)).slice(0, 80);
  const linhas = itens.map((f) => (
    '<tr data-acao="cliente" data-nome="' + esc(f.nome) + '"><td><strong>' + esc(f.nome) + "</strong></td><td>" + esc(fone(f.ddd, f.telefone)) +
    "</td><td>" + esc(f.qtd) + "</td><td>" + esc(dataBr(f.ultima)) + "</td></tr>"
  )).join("");
  return htmlTopo("Farmácias", farmacias().length + " farmácias encontradas no histórico.") +
    '<div class="ferramentas"><input id="busca-cliente" class="busca" placeholder="Nome da farmácia" value="' + esc(ui.buscaCliente) + '"></div>' +
    '<div class="painel" style="overflow:auto"><table class="tabela"><thead><tr><th>Farmácia</th><th>Telefone</th><th>Chamados</th><th>Último</th></tr></thead><tbody>' +
    linhas + "</tbody></table></div>";
}

function htmlRamais() {
  const cartoes = db.ramais.map((r, i) => (
    '<article class="cartao"><strong>' + esc(r.nome) + "</strong><span>Ramal " + esc(r.ramal) + "</span><p>" +
    esc(fone(r.ddd, r.telefone) || "Sem telefone") + "</p><p>" + esc(r.almoco ? "Almoço: " + r.almoco : "") + "</p>" +
    (r.discagem ? '<button class="fantasma" data-acao="copiar" data-texto="' + encodeURIComponent(r.discagem) + '">Copiar discagem</button>' : "") +
    '<button class="perigo" data-acao="delRamal" data-i="' + i + '">Remover</button></article>'
  )).join("");
  const dicas = (db.dicasTelefone || []).map((d) => "<p>" + esc(d) + "</p>").join("");
  return htmlTopo("Ramais", "Telefones internos da Vitória Soft, com horário de almoço.") +
    '<div class="ramais">' + cartoes + "</div>" +
    '<section class="painel" style="margin-top:12px"><h2>Como discar</h2>' + dicas +
    '<div class="colunas"><input id="ramal-nome" placeholder="Nome"><input id="ramal-num" placeholder="Ramal"><input id="ramal-tel" placeholder="Telefone"></div>' +
    '<button class="primario" data-acao="addRamal" style="margin-top:8px">Adicionar ramal</button></section>';
}

function htmlAjustes() {
  const tecnicos = db.config.tecnicos.map((t, i) => (
    '<div class="linha-ajuste"><span>' + esc(t) + '</span><button class="perigo" data-acao="delTecnico" data-i="' + i + '">Remover</button></div>'
  )).join("");
  const canais = db.config.canais.map((c, i) => (
    '<div class="linha-ajuste"><input data-canal="' + i + '" value="' + esc(c.nome) + '"><button class="perigo" data-acao="delCanal" data-i="' + i + '">Remover</button></div>'
  )).join("");
  const frases = db.config.frases.map((f, i) => (
    '<div class="linha-ajuste"><span>' + esc(f.slice(0, 110)) + '</span><button class="perigo" data-acao="delFrase" data-i="' + i + '">Remover</button></div>'
  )).join("");
  const campos = db.config.camposExtras.map((c, i) => (
    '<div class="linha-ajuste"><span>' + esc(c.nome + " (" + c.tipo + ")") + '</span><button class="perigo" data-acao="delCampo" data-i="' + i + '">Remover</button></div>'
  )).join("");
  return htmlTopo("Personalizar", "Canais, técnicos, frases e campos extras ficam salvos neste computador.") +
    '<div class="grade"><section class="painel"><h2>Identificação</h2>' +
    '<div class="grupo"><label>Nome exibido</label><input id="cfg-empresa" value="' + esc(db.config.empresa) + '"></div>' +
    '<div class="grupo"><label>Técnico deste computador</label>' + selectHtml("cfg-tecnico", [["", "Não definido"], ...db.config.tecnicos.map((t) => [t, t])], db.config.tecnicoLocal || "") + "</div>" +
    '<div class="acoes" style="margin-top:10px"><button class="secundario" data-acao="exportar">Exportar planilha</button><button class="secundario" data-acao="importar">Importar Excel</button></div>' +
    '<p>Os dados não saem deste computador. A exportação gera um CSV para abrir no Excel.</p></section>' +
    '<section class="painel"><h2>Canais de entrada</h2><div class="lista-ajustes">' + canais + "</div>" +
    '<div class="ferramentas" style="margin-top:8px"><input id="novo-canal" placeholder="Novo canal"><button class="primario" data-acao="addCanal">Adicionar</button></div></section></div>' +
    '<div class="grade"><section class="painel"><h2>Técnicos</h2><div class="lista-ajustes">' + tecnicos + "</div>" +
    '<div class="ferramentas" style="margin-top:8px"><input id="novo-tecnico" placeholder="Nome"><button class="primario" data-acao="addTecnico">Adicionar</button></div></section>' +
    '<section class="painel"><h2>Campos extras</h2><div class="lista-ajustes">' + (campos || "<p>Nenhum campo extra.</p>") + "</div>" +
    '<div class="colunas" style="margin-top:8px"><input id="campo-nome" placeholder="Nome do campo">' +
    selectHtml("campo-tipo", [["texto", "Texto"], ["lista", "Lista"], ["data", "Data"]], "texto") +
    '<input id="campo-opcoes" placeholder="Opções da lista, separadas por vírgula"></div>' +
    '<button class="primario" data-acao="addCampo" style="margin-top:8px">Adicionar campo</button></section></div>' +
    '<section class="painel" style="margin-top:12px"><h2>Frases prontas do atendimento</h2><div class="lista-ajustes">' + frases + "</div>" +
    '<div class="ferramentas" style="margin-top:8px"><input id="nova-frase" placeholder="Nova frase"><button class="primario" data-acao="addFrase">Adicionar</button></div></section>';
}

function htmlMenu() {
  const item = (tela, nome) => '<button data-acao="tela" data-tela="' + tela + '" class="' + (ui.tela === tela ? "ativo" : "") + '">' + nome + "</button>";
  return '<aside class="menu"><div class="marca"><div class="selo">VS</div><div><strong id="marca-empresa">' + esc(db.config.empresa) + "</strong><span>Atendimento</span></div></div>" +
    '<button class="novo" data-acao="novo">Novo chamado</button>' +
    item("painel", "Painel") + item("fila", "Fila") + item("clientes", "Farmácias") + item("ramais", "Ramais") + item("ajustes", "Personalizar") +
    '<div class="rodape-menu">Ligação, WhatsApp, pedido interno e e-mail no mesmo registro. Atalho: Ctrl+N.</div></aside>';
}

function htmlTela() {
  if (ui.tela === "fila") return htmlFila();
  if (ui.tela === "ficha") return htmlFicha();
  if (ui.tela === "clientes" || ui.tela === "cliente") return htmlClientes();
  if (ui.tela === "ramais") return htmlRamais();
  if (ui.tela === "ajustes") return htmlAjustes();
  return htmlPainel();
}

function render() {
  const ativo = document.activeElement;
  const id = ativo && ativo.id;
  const pos = ativo && typeof ativo.selectionStart === "number" ? ativo.selectionStart : null;
  document.getElementById("app").innerHTML = '<div class="shell">' + htmlMenu() + '<main class="miolo">' + htmlTela() + "</main></div>" +
    (ui.toast ? '<div class="toast">' + esc(ui.toast) + "</div>" : "");
  if (id) {
    const el = document.getElementById(id);
    if (el) {
      el.focus();
      if (pos != null && el.setSelectionRange) el.setSelectionRange(pos, pos);
    }
  }
}

function avisar(texto) {
  ui.toast = texto;
  render();
  setTimeout(() => {
    if (ui.toast === texto) {
      ui.toast = "";
      const toast = document.querySelector(".toast");
      if (toast) toast.remove();
    }
  }, 2400);
}

let gravacao = null;
function persistir() {
  clearTimeout(gravacao);
  gravacao = setTimeout(() => {
    if (window.api) window.api.salvar(db);
    else localStorage.setItem("vs-atendimento", JSON.stringify(db));
  }, 250);
}

function acrescentarLista(chave, id) {
  const el = document.getElementById(id);
  const valor = el ? el.value.trim() : "";
  if (!valor) return;
  db.config[chave].push(valor);
  persistir();
  render();
}

function removerLista(chave, indice) {
  db.config[chave].splice(indice, 1);
  persistir();
  render();
}

function acrescentarCanal() {
  const nome = document.getElementById("novo-canal").value.trim();
  if (!nome) return;
  const id = nome.toLocaleLowerCase("pt-BR").normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[^a-z0-9]+/g, "-");
  db.config.canais.push({ id: id || "canal-" + Date.now(), nome, cor: "#0f6e62" });
  persistir();
  render();
}

function acrescentarCampo() {
  const nome = document.getElementById("campo-nome").value.trim();
  if (!nome) return;
  db.config.camposExtras.push({
    id: "c" + Date.now(),
    nome,
    tipo: document.getElementById("campo-tipo").value,
    opcoes: document.getElementById("campo-opcoes").value,
  });
  persistir();
  render();
}

function acrescentarRamal() {
  const nome = document.getElementById("ramal-nome").value.trim();
  const ramal = document.getElementById("ramal-num").value.trim();
  if (!nome || !ramal) return avisar("Informe o nome e o ramal.");
  db.ramais.push({ ramal, nome, almoco: "", ddd: "", telefone: document.getElementById("ramal-tel").value.trim(), discagem: "" });
  persistir();
  render();
}

function csvCampo(valor) {
  const texto = String(valor ?? "").replace(/"/g, '""');
  return '"' + texto + '"';
}

async function exportar() {
  const cabecalho = ["Protocolo", "Data", "Hora início", "Hora fim", "Status", "Canal", "Cliente", "Farmácia", "Código", "Técnico", "Assunto", "DDD", "Telefone", "Telefone 2", "Observações", "Agendamento", "Prioridade"];
  const linhas = db.chamados.map((c) => [
    c.protocolo, dataBr(c.data), c.horaInicio, c.horaFim, statusDe(c.status).nome, canalDe(c.canal).nome,
    c.cliente, c.farmacia, c.codigo, c.tecnico, c.assunto, c.ddd, c.telefone, c.telefone2, c.obs, c.agendamento, c.prioridade,
  ].map(csvCampo).join(";"));
  const conteudo = "\uFEFF" + cabecalho.join(";") + "\n" + linhas.join("\n");
  if (window.api) {
    const ok = await window.api.exportar("atendimento-vitoria-soft.csv", conteudo);
    if (ok) avisar("Planilha exportada.");
  } else {
    const a = document.createElement("a");
    a.href = URL.createObjectURL(new Blob([conteudo], { type: "text/csv;charset=utf-8" }));
    a.download = "atendimento-vitoria-soft.csv";
    a.click();
  }
}

async function importarPlanilha() {
  if (!window.api) return avisar("A importação de Excel fica disponível no programa instalado.");
  const resposta = await window.api.importar();
  if (!resposta || resposta.cancelado) return;
  db = resposta.dados;
  cacheFarmacias = null;
  avisar(resposta.novos + " chamados novos importados.");
  render();
}

function abrir(url) {
  if (window.api) window.api.abrir(url);
  else window.open(url, "_blank");
}

document.body.addEventListener("click", (evento) => {
  const alvo = evento.target.closest("[data-acao]");
  if (!alvo || !db) return;
  const fn = acoes[alvo.dataset.acao];
  if (fn) fn(alvo.dataset);
});

document.body.addEventListener("input", (evento) => {
  if (!db) return;
  if (evento.target.id === "busca") {
    ui.busca = evento.target.value;
    ui.pagina = 0;
    render();
  }
  if (evento.target.id === "busca-cliente") {
    ui.buscaCliente = evento.target.value;
    ui.farmaciaAberta = "";
    render();
  }
  if (evento.target.id === "f-farmacia" && ui.rascunho) {
    lerRascunhoDoFormulario();
    const farm = acharFarmacia(ui.rascunho.farmacia);
    if (farm && !ui.rascunho.telefone) {
      ui.rascunho.ddd = farm.ddd;
      ui.rascunho.telefone = farm.telefone;
      ui.rascunho.telefone2 = farm.telefone2;
      ui.rascunho.codigo = farm.codigo;
    }
    render();
  }
});

document.body.addEventListener("change", (evento) => {
  if (!db) return;
  const id = evento.target.id;
  if (id === "filtro-status") { ui.filtroStatus = evento.target.value; ui.pagina = 0; render(); }
  if (id === "filtro-canal") { ui.filtroCanal = evento.target.value; ui.pagina = 0; render(); }
  if (id === "filtro-tecnico") { ui.filtroTecnico = evento.target.value; ui.pagina = 0; render(); }
  if (id === "cfg-empresa") { db.config.empresa = evento.target.value; persistir(); }
  if (id === "cfg-tecnico") { db.config.tecnicoLocal = evento.target.value; persistir(); }
  if (evento.target.dataset.canal != null) {
    db.config.canais[Number(evento.target.dataset.canal)].nome = evento.target.value;
    persistir();
  }
});

document.body.addEventListener("submit", (evento) => evento.preventDefault());

document.addEventListener("keydown", (evento) => {
  if (evento.ctrlKey && evento.key.toLowerCase() === "n") {
    evento.preventDefault();
    novoChamado();
  }
});

async function iniciar() {
  if (window.api) db = await window.api.carregar();
  else {
    const salvo = localStorage.getItem("vs-atendimento");
    if (salvo) db = JSON.parse(salvo);
    else {
      const resposta = await fetch("../seed/dados.json");
      db = await resposta.json();
    }
  }
  db.config.camposExtras = db.config.camposExtras || [];
  db.ramais = db.ramais || [];
  db.dicasTelefone = db.dicasTelefone || [];
  render();
}

iniciar().catch((erro) => {
  document.getElementById("app").textContent = "Não foi possível abrir os dados do atendimento. " + erro.message;
});
