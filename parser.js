const fs = require("fs");
const path = require("path");

function decode(s) {
  return String(s)
    .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n)))
    .replace(/&#x([0-9a-fA-F]+);/g, (_, n) => String.fromCodePoint(parseInt(n, 16)))
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
}

function parseSharedStrings(xml) {
  const out = [];
  let i = 0;
  while (i < xml.length) {
    const s = xml.indexOf("<si>", i);
    if (s < 0) break;
    const e = xml.indexOf("</si>", s);
    if (e < 0) break;
    const block = xml.slice(s + 4, e);
    let text = "";
    let t = 0;
    while (t < block.length) {
      const a = block.indexOf("<t", t);
      if (a < 0) break;
      const gt = block.indexOf(">", a);
      if (gt < 0) break;
      const close = block.indexOf("</t>", gt);
      if (close < 0) break;
      text += decode(block.slice(gt + 1, close));
      t = close + 4;
    }
    out.push(text);
    i = e + 5;
  }
  return out;
}

function cellsOf(chunk) {
  const map = {};
  let i = 0;
  while (i < chunk.length) {
    const s = chunk.indexOf('<c r="', i);
    if (s < 0) break;
    const q2 = chunk.indexOf('"', s + 6);
    if (q2 < 0) break;
    const ref = chunk.slice(s + 6, q2);
    const col = ref.replace(/[0-9]/g, "");
    const gt = chunk.indexOf(">", q2);
    if (gt < 0) break;
    if (chunk[gt - 1] === "/") {
      i = gt + 1;
      continue;
    }
    const end = chunk.indexOf("</c>", gt);
    if (end < 0) break;
    if (col.length === 1 && col >= "A" && col <= "Z") {
      const tag = chunk.slice(q2, gt);
      const inner = chunk.slice(gt + 1, end);
      const v0 = inner.indexOf("<v>");
      if (v0 >= 0) {
        const v1 = inner.indexOf("</v>", v0);
        if (v1 > v0) {
          map[col] = {
            shared: tag.includes('t="s"'),
            raw: inner.slice(v0 + 3, v1),
          };
        }
      }
    }
    i = end + 4;
  }
  return map;
}

function rowsOf(xml) {
  const rows = [];
  let i = 0;
  while (i < xml.length) {
    const s = xml.indexOf("<row ", i);
    if (s < 0) break;
    const gt = xml.indexOf(">", s);
    if (gt < 0) break;
    const end = xml.indexOf("</row>", gt);
    if (end < 0) break;
    const head = xml.slice(s, gt);
    const num = /r="(\d+)"/.exec(head);
    rows.push({ n: num ? Number(num[1]) : rows.length + 1, cells: cellsOf(xml.slice(gt + 1, end)) });
    i = end + 6;
  }
  return rows;
}

function excelDate(serial) {
  const n = Number(serial);
  if (!Number.isFinite(n) || n < 20000 || n > 80000) return "";
  const ms = Date.UTC(1899, 11, 30) + Math.floor(n) * 86400000;
  return new Date(ms).toISOString().slice(0, 10);
}

function excelTime(serial) {
  const n = Number(serial);
  if (!Number.isFinite(n) || n <= 0 || n >= 1) return "";
  const total = Math.round(n * 24 * 60);
  const h = Math.floor(total / 60) % 24;
  const m = total % 60;
  return String(h).padStart(2, "0") + ":" + String(m).padStart(2, "0");
}

function textOf(cell, strings) {
  if (!cell) return "";
  if (cell.shared) {
    const idx = Number(cell.raw);
    return strings[idx] != null ? String(strings[idx]).trim() : "";
  }
  return String(cell.raw).trim();
}

function asNumber(cell) {
  if (!cell || cell.shared) return null;
  const n = Number(cell.raw);
  return Number.isFinite(n) ? n : null;
}

function smallInt(value) {
  if (value == null || value === "") return null;
  const n = Number(String(value).replace(",", "."));
  if (!Number.isInteger(n)) return null;
  return n;
}

function phoneDigits(value) {
  if (value == null || value === "") return "";
  const n = Number(value);
  if (Number.isFinite(n) && String(value).indexOf(".") === -1 && n > 1000) {
    return String(Math.round(n));
  }
  const digits = String(value).replace(/\D/g, "");
  return digits;
}

function canalDe(texto) {
  const s = String(texto || "").toLowerCase();
  if (/whats|wpp|\bzap\b|vschat|vs-chat|vs chat/.test(s)) return "whatsapp";
  if (/e-?mail/.test(s)) return "email";
  if (/pedido interno|internamente/.test(s)) return "interno";
  if (/liguei|liga[cç][aã]o|ngm atende|ningu[eé]m atende|n[aã]o atendeu/.test(s)) return "ligacao";
  return "";
}

function textoUtil(valor) {
  const t = String(valor || "").trim();
  if (!t) return "";
  if (/^\d+(\.\d+)?$/.test(t) && Number(t) > 20000 && Number(t) < 80000) return "";
  return t;
}

function horaDe(cell, strings) {
  const n = asNumber(cell);
  const convertida = excelTime(n);
  if (convertida) return convertida;
  const t = textOf(cell, strings);
  if (/^\d+\.\d+$/.test(t)) return excelTime(Number(t)) || "";
  return t;
}

function prioridadeDe(texto) {
  return /urgente/i.test(texto || "") ? "alta" : "normal";
}

function extrairAgendamento(obs, ano, mes, dia) {
  const s = String(obs || "");
  let m = /ligar\s+dia\s+(\d{1,2})\s*\/\s*(\d{1,2})(?:\s*(?:as|às|a)?\s*(\d{1,2})\s*(?:[:h]\s*(\d{2}))?)?/i.exec(s);
  if (!m) m = /ligar\s+(?:as|às|após as|a partir das|a partir de)\s*(\d{1,2})\s*(?:[:h]\s*(\d{2}))?/i.exec(s);
  if (!m) return "";
  let dd = dia;
  let mm = mes;
  let anoAg = ano;
  let hh = "09";
  let mi = "00";
  if (m.length >= 4 && m[2] && Number(m[2]) <= 12 && Number(m[1]) <= 31 && /dia/i.test(m[0])) {
    dd = Number(m[1]);
    mm = Number(m[2]);
    if (m[3]) hh = String(m[3]).padStart(2, "0");
    if (m[4]) mi = String(m[4]).padStart(2, "0");
  } else if (m[1] && Number(m[1]) <= 23) {
    hh = String(m[1]).padStart(2, "0");
    mi = m[2] ? String(m[2]).padStart(2, "0") : "00";
  }
  if (mm && mes && mm <= 3 && mes >= 10) anoAg = Number(ano) + 1;
  if (!dd || !mm || !anoAg) return "";
  const diaMax = new Date(anoAg, mm, 0).getDate();
  if (dd > diaMax) return "";
  return `${anoAg}-${String(mm).padStart(2, "0")}-${String(dd).padStart(2, "0")}T${hh}:${mi}`;
}

function statusDe(ok, obs, agendamento, dataIso) {
  const texto = String(obs || "");
  const concluidoNota = /j[aá] foi resolvid|j[aá] resolveu|n[aã]o precisa mais|cancelou/i.test(texto);
  if (/^ok$/i.test(String(ok || "").trim()) || concluidoNota) return "concluido";
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  if (agendamento) {
    const quando = new Date(agendamento);
    if (!Number.isNaN(quando.getTime()) && quando >= hoje) return "agendado";
  }
  if (dataIso) {
    const d = new Date(dataIso + "T12:00:00");
    if (hoje.getTime() - d.getTime() > 21 * 86400000) return "concluido";
  } else {
    return "concluido";
  }
  if (/ligar|retorn|ngm atende|ningu[eé]m atende|n[aã]o atendeu|remarcar/i.test(texto)) return "retorno";
  return "aberto";
}

function registrarTecnico(map, nome) {
  const limpo = String(nome || "").replace(/\s+/g, " ").trim();
  if (limpo.length < 2 || /^\d+$/.test(limpo)) return;
  const partes = limpo.includes("/") ? limpo.split(/\s*\/\s*/) : [limpo];
  for (const parte of partes) {
    if (parte.length < 2 || /^\d+$/.test(parte)) continue;
    const key = parte.toLocaleUpperCase("pt-BR").normalize("NFD").replace(/[\u0300-\u036f]/g, "");
    const atual = map.get(key) || { nome: parte, qtd: 0 };
    atual.qtd += 1;
    if (parte !== parte.toLocaleUpperCase("pt-BR") && atual.nome === atual.nome.toLocaleUpperCase("pt-BR")) atual.nome = parte;
    map.set(key, atual);
  }
}

function dataNaNota(obs) {
  const m = /(\d{1,2})\s*\/\s*(\d{1,2})/.exec(String(obs || ""));
  if (!m) return null;
  const dia = Number(m[1]);
  const mes = Number(m[2]);
  if (dia < 1 || dia > 31 || mes < 1 || mes > 12) return null;
  return { dia, mes };
}

function modo(valores) {
  const conta = new Map();
  for (const valor of valores) {
    if (!valor) continue;
    conta.set(valor, (conta.get(valor) || 0) + 1);
  }
  let melhor = null;
  let quantidade = -1;
  for (const [valor, qtd] of conta) {
    if (qtd > quantidade) {
      melhor = valor;
      quantidade = qtd;
    }
  }
  return melhor;
}

function aplicarDatas(chamados) {
  const fonte = chamados.map((c) => c.mesCol || c.mesNota || null);
  const suave = fonte.map((_, i) => {
    const fatia = [];
    const inicio = Math.max(0, i - 25);
    const fim = Math.min(fonte.length, i + 26);
    for (let j = inicio; j < fim; j++) if (fonte[j]) fatia.push(fonte[j]);
    return modo(fatia);
  });

  let ano = 2024;
  let comprometido = null;
  const memoria = [];
  let ultima = "";
  let ultimoIncremento = -1000;
  for (let i = 0; i < chamados.length; i++) {
    const c = chamados[i];
    const consenso = suave[i];
    if (consenso) {
      memoria.push(consenso);
      if (memoria.length > 180) memoria.shift();
    }
    const tendencia = modo(memoria);
    if (tendencia && comprometido != null && comprometido >= 11 && tendencia <= 2 && i - ultimoIncremento > 500) {
      ano += 1;
      ultimoIncremento = i;
    }
    if (tendencia) comprometido = tendencia;

    let dia = c.diaCol || null;
    let mes = c.mesCol || null;
    if (!mes && c.mesNota) {
      dia = c.diaNota;
      mes = c.mesNota;
    }
    if (mes && consenso && mes !== consenso) {
      const vizinho = Math.abs(mes - consenso) <= 1 || (mes <= 2 && consenso >= 11) || (consenso <= 2 && mes >= 11);
      if (!vizinho) mes = consenso;
    }
    if (!mes) mes = consenso;
    let anoLinha = ano;
    if (mes && comprometido != null && comprometido <= 4 && mes >= 10) anoLinha = ano - 1;
    if (dia && mes) {
      const maximo = new Date(anoLinha, mes, 0).getDate();
      if (dia >= 1 && dia <= maximo) {
        c.data = anoLinha + "-" + String(mes).padStart(2, "0") + "-" + String(dia).padStart(2, "0");
        ultima = c.data;
      }
    }
    if (!c.data && ultima) c.data = ultima;
    if (c.obs) {
      const nota = dataNaNota(c.obs);
      if (nota) c.agendamento = c.agendamento || extrairAgendamento(c.obs, anoLinha, mes || nota.mes, nota.dia);
    }
    delete c.diaCol;
    delete c.mesCol;
    delete c.diaNota;
    delete c.mesNota;
  }
}

function montarChamados(rows, strings) {
  const chamados = [];
  const tecnicos = new Map();
  const datasAncora = [];

  for (const row of rows) {
    if (row.n === 1) continue;
    const c = row.cells;
    const ancora = excelDate(asNumber(c.A));
    if (ancora && !datasAncora.length) datasAncora.push(ancora);

    const cliente = textoUtil(textOf(c.C, strings));
    const farmacia = textoUtil(textOf(c.D, strings));
    const assunto = textoUtil(textOf(c.G, strings));
    const telefone = phoneDigits(textOf(c.I, strings));
    if (!cliente && !farmacia && !assunto && !telefone) continue;
    if (/^(cliente|farmacia|assunto)$/i.test(cliente)) continue;

    const ok = textOf(c.A, strings);
    const horaInicio = horaDe(c.B, strings);
    const horaFim = horaDe(c.K, strings);
    const tecnico = textOf(c.F, strings);
    const obs = textOf(c.L, strings);
    const marcador = textOf(c.E, strings);

    let dia = smallInt(textOf(c.O, strings));
    let mes = smallInt(textOf(c.P, strings));
    let ticket = textOf(c.M, strings);
    let codigo = textOf(c.N, strings);
    if (!(dia >= 1 && dia <= 31 && mes >= 1 && mes <= 12)) {
      const m = smallInt(ticket);
      const n = smallInt(codigo);
      if (m >= 1 && m <= 31 && n >= 1 && n <= 12) {
        dia = m;
        mes = n;
        ticket = "";
        codigo = "";
      } else {
        dia = null;
        mes = null;
      }
    }
    const nota = dataNaNota(obs);
    const ddd = phoneDigits(textOf(c.H, strings));
    const telefone2 = phoneDigits(textOf(c.J, strings));
    const agCol = textOf(c.Q, strings);
    const agNum = excelDate(asNumber(c.Q));
    const agendamento = agNum || (/^\d{4}-\d{2}-\d{2}/.test(agCol) ? agCol : "");
    const blob = [assunto, obs, horaInicio, marcador].join(" ");
    const canal = canalDe(blob);
    const idNum = chamados.length + 1;

    registrarTecnico(tecnicos, tecnico);

    chamados.push({
      id: "imp-" + row.n,
      protocolo: "AT-" + String(idNum).padStart(5, "0"),
      origemLinha: row.n,
      canal,
      canalInformado: false,
      data: "",
      diaCol: dia,
      mesCol: mes,
      diaNota: nota && nota.dia,
      mesNota: nota && nota.mes,
      horaInicio,
      horaFim,
      status: "aberto",
      okImportado: ok,
      cliente,
      farmacia,
      codigo,
      tecnico,
      assunto,
      ddd,
      telefone,
      telefone2,
      obs,
      agendamento,
      prioridade: prioridadeDe(assunto + " " + obs),
      marcador: /^x$/i.test(marcador) ? "X" : marcador,
      ticket,
      historico: obs ? [{ em: "", texto: obs, tecnico }] : [],
      extras: {},
      criadoEm: "",
      atualizadoEm: "",
    });
  }

  aplicarDatas(chamados);
  for (const c of chamados) {
    const hora = /^\d{2}:\d{2}$/.test(c.horaInicio) ? c.horaInicio : "08:00";
    c.criadoEm = c.data ? c.data + "T" + hora : "";
    c.atualizadoEm = c.data || "";
    if (c.historico[0]) c.historico[0].em = c.data || "";
  }

  for (const c of chamados) {
    c.status = statusDe(c.okImportado, c.obs, c.agendamento, c.data);
    delete c.okImportado;
  }

  return {
    chamados,
    tecnicos: [...tecnicos.values()]
      .sort((a, b) => b.qtd - a.qtd)
      .map((t) => t.nome.replace(/\s+/g, " ").trim())
      .map((nome) => nome.toLowerCase().replace(/(^|[\s/])(\p{L})/gu, (_, sep, letra) => sep + letra.toUpperCase())),
    datasAncora,
    saltos: [],
  };
}

function sheetByName(dir) {
  const wb = fs.readFileSync(path.join(dir, "workbook.xml"), "utf8");
  const rels = fs.readFileSync(path.join(dir, "_rels", "workbook.xml.rels"), "utf8");
  const relMap = {};
  const relRe = /Id="([^"]+)"[^>]*Target="([^"]+)"/g;
  let m;
  while ((m = relRe.exec(rels))) relMap[m[1]] = m[2];
  const sheets = [];
  const sh = /name="([^"]+)"[^>]*r:id="([^"]+)"/g;
  while ((m = sh.exec(wb))) {
    const target = relMap[m[2]];
    if (!target) continue;
    sheets.push({ name: m[1], file: path.join(dir, target.replace(/\//g, path.sep)) });
  }
  return sheets;
}

function ramaisDe(rows, strings) {
  const itens = [];
  const dicas = [];
  for (const row of rows) {
    const codigo = textOf(row.cells.A, strings);
    const nome = textOf(row.cells.B, strings).trim();
    const aviso = textOf(row.cells.G, strings).trim();
    if (/^\d{3,5}$/.test(codigo) && nome && !/ramal|hora atual/i.test(codigo + nome)) {
      itens.push({
        ramal: codigo,
        nome,
        almoco: textOf(row.cells.C, strings).trim(),
        ddd: textOf(row.cells.H, strings).replace(/\D/g, ""),
        telefone: textOf(row.cells.I, strings).replace(/\D/g, ""),
        discagem: textOf(row.cells.J, strings).replace(/\D/g, ""),
      });
    } else if (aviso.length > 12 && !/^(operadora|numero)/i.test(aviso)) {
      dicas.push(aviso);
    }
  }
  return { itens, dicas };
}

function frasesDaAba(rows, strings) {
  const frases = [];
  const vistos = new Set();
  for (const row of rows) {
    for (const cell of Object.values(row.cells)) {
      const t = textOf(cell, strings).replace(/\r/g, "").trim();
      if (t.length < 30 || vistos.has(t)) continue;
      vistos.add(t);
      frases.push(t);
    }
  }
  return frases;
}

function tabelaGenerica(rows, strings) {
  let header = null;
  const itens = [];
  for (const row of rows) {
    const vals = {};
    for (const [col, cell] of Object.entries(row.cells)) {
      const t = textOf(cell, strings);
      if (t) vals[col] = t;
    }
    if (!Object.keys(vals).length) continue;
    if (!header && Object.keys(vals).length >= 2) {
      header = vals;
      continue;
    }
    if (!header) continue;
    const item = {};
    for (const [col, titulo] of Object.entries(header)) {
      if (vals[col]) item[titulo.replace(/\s+/g, " ").trim()] = vals[col];
    }
    if (Object.keys(item).length) itens.push(item);
  }
  return { header, itens };
}

function frasesDe(itens) {
  const frases = [];
  const vistos = new Set();
  for (const item of itens) {
    for (const valor of Object.values(item)) {
      const t = String(valor || "").trim();
      if (t.length < 25) continue;
      if (vistos.has(t)) continue;
      vistos.add(t);
      frases.push(t);
    }
  }
  return frases;
}

function dadosDePlanilha(xlDir) {
  const strings = parseSharedStrings(fs.readFileSync(path.join(xlDir, "sharedStrings.xml"), "utf8"));
  const sheets = sheetByName(xlDir);
  const principal = sheets.find((s) => s.name === "Plan2") || sheets[0];
  const rows = rowsOf(fs.readFileSync(principal.file, "utf8"));
  const montado = montarChamados(rows, strings);
  const ramaisSheet = sheets.find((s) => /ramal/i.test(s.name));
  const chatSheet = sheets.find((s) => /vschat|chat/i.test(s.name));
  const ramais = ramaisSheet ? ramaisDe(rowsOf(fs.readFileSync(ramaisSheet.file, "utf8")), strings) : { itens: [], dicas: [] };
  if (!ramais.itens.length) {
    ramais.itens = [
      { ramal: "5000", nome: "Recepção", almoco: "11h às 13h", ddd: "67", telefone: "33869704", discagem: "0216733869704" },
      { ramal: "3122", nome: "André", almoco: "12h às 14h", ddd: "19", telefone: "30371616", discagem: "0211930371616" },
      { ramal: "3123", nome: "Wigner", almoco: "13h às 15h", ddd: "24", telefone: "999460879", discagem: "02124999460879" },
      { ramal: "3124", nome: "Jefferson", almoco: "12h às 14h", ddd: "24", telefone: "999460879", discagem: "02124999460879" },
      { ramal: "3125", nome: "Rafael/Kevin", almoco: "", ddd: "48", telefone: "988011100", discagem: "02148988011100" },
      { ramal: "3126", nome: "Batista", almoco: "11h às 13h", ddd: "67", telefone: "33255851", discagem: "0216733255851" },
      { ramal: "3127", nome: "Macnair", almoco: "12h às 14h", ddd: "51", telefone: "996467879", discagem: "02151996467879" },
    ];
    ramais.dicas = [
      "Metropolitano: não precisa do DDD nem do 021.",
      "Interurbano: disque 021 + DDD + número. Exemplo: 0215134881010.",
      "Para inserir a hora atual na planilha antiga: Ctrl + Shift + ;",
    ];
  }
  const frasesPlanilha = chatSheet ? frasesDaAba(rowsOf(fs.readFileSync(chatSheet.file, "utf8")), strings) : [];

  return {
    versao: 1,
    config: {
      empresa: "Vitória Soft",
      tecnicoLocal: "",
      canais: [
        { id: "ligacao", nome: "Ligação", cor: "#1d4ed8" },
        { id: "whatsapp", nome: "WhatsApp", cor: "#15803d" },
        { id: "interno", nome: "Pedido interno", cor: "#b45309" },
        { id: "email", nome: "E-mail", cor: "#6d28d9" },
      ],
      status: [
        { id: "aberto", nome: "Aberto", cor: "#b91c1c" },
        { id: "andamento", nome: "Em atendimento", cor: "#1d4ed8" },
        { id: "retorno", nome: "Aguardando retorno", cor: "#b45309" },
        { id: "agendado", nome: "Agendado", cor: "#6d28d9" },
        { id: "concluido", nome: "Concluído", cor: "#15803d" },
      ],
      tecnicos: montado.tecnicos,
      frases: [
        "Aguarde só um momento.",
        "Ninguém atendeu.",
        "Deixei recado.",
        "Cliente pediu para remarcar.",
        "Já foi resolvido.",
        ...frasesPlanilha,
      ],
      camposExtras: [],
      camposOcultos: [],
    },
    chamados: montado.chamados,
    ramais: ramais.itens,
    dicasTelefone: ramais.dicas || [],
    meta: {
      datasAncora: montado.datasAncora.slice(0, 12),
      saltos: montado.saltos,
      abas: sheets.map((s) => s.name),
    },
  };
}

function resumir(dados) {
  const datas = dados.chamados.map((c) => c.data).filter(Boolean).sort();
  const canais = {};
  const status = {};
  for (const c of dados.chamados) {
    canais[c.canal] = (canais[c.canal] || 0) + 1;
    status[c.status] = (status[c.status] || 0) + 1;
  }
  return {
    chamados: dados.chamados.length,
    de: datas[0] || "",
    ate: datas[datas.length - 1] || "",
    canais,
    status,
    tecnicos: dados.config.tecnicos.slice(0, 15),
    ramais: dados.ramais.length,
    frases: dados.config.frases.length,
    amostra: dados.chamados.slice(0, 2).map((c) => ({ data: c.data, hi: c.horaInicio, hf: c.horaFim, cliente: c.cliente, farmacia: c.farmacia })),
    ultimo: dados.chamados.slice(-2).map((c) => ({ data: c.data, hi: c.horaInicio, hf: c.horaFim, cliente: c.cliente, assunto: c.assunto })),
    ramalAmostra: dados.ramais.slice(0, 6),
    ancoras: (dados.meta.datasAncora || []).length,
    saltos: (dados.meta.saltos || []).slice(0, 25),
    saltosTotal: (dados.meta.saltos || []).length,
  };
}

function inspecionar(xlDir, inicio, fim) {
  const strings = parseSharedStrings(fs.readFileSync(path.join(xlDir, "sharedStrings.xml"), "utf8"));
  const sheets = sheetByName(xlDir);
  for (const sheet of sheets) {
    if (sheet.name === "Plan2") continue;
    const rows = rowsOf(fs.readFileSync(sheet.file, "utf8")).slice(0, 20);
    console.log("\nABA", sheet.name, "linhas", rows.length);
    for (const row of rows) {
      const vals = {};
      for (const [col, cell] of Object.entries(row.cells)) {
        const t = textOf(cell, strings);
        if (t) vals[col] = t.slice(0, 80);
      }
      if (Object.keys(vals).length) console.log(row.n, JSON.stringify(vals));
    }
  }
  const principal = sheets.find((s) => s.name === "Plan2");
  const rows = rowsOf(fs.readFileSync(principal.file, "utf8"));
  for (const row of rows) {
    if (row.n < inicio || row.n > fim) continue;
    const vals = {};
    for (const col of ["A", "B", "C", "D", "E", "F", "G", "H", "I", "K", "L", "M", "N", "O", "P", "Q"]) {
      const t = textOf(row.cells[col], strings);
      if (t) vals[col] = t.slice(0, 60);
    }
    console.log(row.n, JSON.stringify(vals));
  }
}

if (require.main === module) {
  const xlDir = process.argv[2];
  const outFile = process.argv[3];
  if (!xlDir || !outFile) {
    console.error("Uso: node parser.js <pasta xl> <saida.json>");
    process.exit(1);
  }
  if (process.argv[4] === "--ver") {
    inspecionar(xlDir, Number(process.argv[5] || 2620), Number(process.argv[6] || 2660));
    process.exit(0);
  }
  const dados = dadosDePlanilha(xlDir);
  fs.mkdirSync(path.dirname(outFile), { recursive: true });
  fs.writeFileSync(outFile, JSON.stringify(dados));
  console.log(JSON.stringify(resumir(dados), null, 2));
}

module.exports = { dadosDePlanilha, resumir };
