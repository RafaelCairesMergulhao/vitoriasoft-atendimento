# Vitória Soft Atendimento

Central de atendimento para a recepção. A pessoa da recepção vê o painel do dia, abre e edita chamados, confirma atendimentos e importa a planilha diária.

O projeto tem duas interfaces do mesmo sistema:

- `renderer/` e `electron/` — aplicativo de desktop com Electron
- `nativo/` — aplicativo Windows em C# com Windows Forms

## Tecnologias

JavaScript, HTML, CSS, Electron, C# e .NET.

## Como rodar a versão Electron

É preciso ter o [Node.js](https://nodejs.org) instalado.

```powershell
npm install
npm start
```

## Como rodar a versão Windows

É preciso ter o [.NET SDK](https://dotnet.microsoft.com/download) instalado.

```powershell
dotnet run --project nativo
```

A planilha de atendimentos e os instaladores gerados ficam fora do repositório.
