using System.IO;

namespace MouseTool;

internal static class HelpFileWriter
{
    public static void EnsureHelpFiles(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        foreach (var help in GetHelpDefinitions())
        {
            File.WriteAllText(GetHelpFilePath(directoryPath, help.LanguageCode), BuildHelpHtml(help));
        }
    }

    public static string GetHelpFilePath(string directoryPath, string languageCode)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "en",
            "pt-BR",
            "fr",
            "es",
            "de",
            "it",
            "ru"
        };

        var normalized = supported.Contains(languageCode) ? languageCode : "en";
        return Path.Combine(directoryPath, $"HELP.{normalized}.html");
    }

    private static string BuildHelpHtml(HelpContent help)
    {
        var listItems = string.Join(Environment.NewLine, help.HowToSteps.Select(step => $"      <li>{step}</li>"));

        return $$"""
<!doctype html>
<html lang="{{help.LanguageCode}}">
<head>
  <meta charset="utf-8">
  <title>{{help.Title}}</title>
  <style>
    body { font-family: Segoe UI, sans-serif; margin: 40px; color: #243041; background: #f5f7fa; }
    .card { background: white; border-radius: 16px; padding: 28px; margin-bottom: 24px; box-shadow: 0 8px 28px rgba(14, 32, 56, 0.08); }
    h1, h2 { color: #12304d; }
    p, li { line-height: 1.6; }
  </style>
</head>
<body>
  <div class="card">
    <h1>{{help.Title}}</h1>
    <p>{{help.Intro}}</p>
  </div>
  <div class="card">
    <h2>{{help.HowToTitle}}</h2>
    <ol>
{{listItems}}
    </ol>
  </div>
  <div class="card">
    <h2>{{help.LanguageSectionTitle}}</h2>
    <p>{{help.LanguageSectionBody1}}</p>
    <p>{{help.LanguageSectionBody2}}</p>
  </div>
  <div class="card">
    <h2>{{help.TouchAccessTitle}}</h2>
    <p>{{help.TouchAccessBody}}</p>
  </div>
</body>
</html>
""";
    }

    private static IReadOnlyList<HelpContent> GetHelpDefinitions()
    {
        return
        [
            new HelpContent(
                "en",
                "MouseTool Help",
                "MouseTool keeps your mouse anchored to the main display while a touchscreen works on another monitor.",
                "How to Use",
                [
                    "Select the correct primary display and touchscreen display in the Displays tab.",
                    "Click Apply Changes.",
                    "Use the physical mouse on the main display.",
                    "Use touch on the touchscreen display.",
                    "Move the physical mouse again to return quickly to the last saved position on the main display."
                ],
                "Language and Diagnostics",
                "The app defaults to the system language, but you can change the interface language from the top selector.",
                "Use the Diagnostics tab only when you need logs for testing or troubleshooting.",
                "Touchscreen Mouse Access",
                "In the Behavior tab, you can allow or block the physical mouse from entering the monitor configured as the touchscreen area."
            ),
            new HelpContent(
                "pt-BR",
                "Ajuda do MouseTool",
                "O MouseTool mantem o mouse ancorado na tela principal enquanto a tela touchscreen funciona em outro monitor.",
                "Como usar",
                [
                    "Selecione corretamente a tela principal e a tela touchscreen na aba Telas.",
                    "Clique em Aplicar Alteracoes.",
                    "Use o mouse fisico na tela principal.",
                    "Use o toque na tela touchscreen.",
                    "Mova o mouse fisico novamente para voltar rapidamente para a ultima posicao salva na tela principal."
                ],
                "Idioma e diagnostico",
                "O aplicativo usa por padrao o idioma do sistema, mas voce pode alterar o idioma da interface pelo seletor no topo da janela.",
                "Use a aba Diagnostico somente quando precisar de logs para testes ou suporte.",
                "Acesso do mouse a tela touchscreen",
                "Na aba Comportamento, voce pode permitir ou bloquear que o mouse fisico entre no monitor configurado como area touchscreen."
            ),
            new HelpContent(
                "fr",
                "Aide MouseTool",
                "MouseTool maintient la souris ancree sur l'ecran principal pendant qu'un ecran tactile fonctionne sur un autre moniteur.",
                "Comment utiliser",
                [
                    "Selectionnez correctement l'ecran principal et l'ecran tactile dans l'onglet Ecrans.",
                    "Cliquez sur Appliquer les modifications.",
                    "Utilisez la souris physique sur l'ecran principal.",
                    "Utilisez le tactile sur l'ecran tactile.",
                    "Deplacez de nouveau la souris physique pour revenir rapidement a la derniere position enregistree sur l'ecran principal."
                ],
                "Langue et diagnostic",
                "L'application utilise par defaut la langue du systeme, mais vous pouvez modifier la langue de l'interface avec le selecteur en haut de la fenetre.",
                "Utilisez l'onglet Diagnostic uniquement lorsque vous avez besoin de journaux pour les tests ou le support.",
                "Acces de la souris a l'ecran tactile",
                "Dans l'onglet Comportement, vous pouvez autoriser ou bloquer l'entree de la souris physique dans le moniteur configure comme zone tactile."
            ),
            new HelpContent(
                "es",
                "Ayuda de MouseTool",
                "MouseTool mantiene el raton anclado en la pantalla principal mientras una pantalla tactil funciona en otro monitor.",
                "Como usar",
                [
                    "Selecciona correctamente la pantalla principal y la pantalla tactil en la pestana Pantallas.",
                    "Haz clic en Aplicar cambios.",
                    "Usa el raton fisico en la pantalla principal.",
                    "Usa el tactil en la pantalla tactil.",
                    "Mueve de nuevo el raton fisico para volver rapidamente a la ultima posicion guardada en la pantalla principal."
                ],
                "Idioma y diagnostico",
                "La aplicacion usa por defecto el idioma del sistema, pero puedes cambiar el idioma de la interfaz desde el selector de la parte superior.",
                "Usa la pestana Diagnostico solo cuando necesites registros para pruebas o soporte.",
                "Acceso del raton a la pantalla tactil",
                "En la pestana Comportamiento, puedes permitir o bloquear que el raton fisico entre en el monitor configurado como zona tactil."
            ),
            new HelpContent(
                "de",
                "MouseTool Hilfe",
                "MouseTool haelt die Maus am Hauptbildschirm verankert, waehrend ein Touchscreen auf einem anderen Monitor arbeitet.",
                "Verwendung",
                [
                    "Waehlen Sie auf der Registerkarte Bildschirme den richtigen Hauptbildschirm und den richtigen Touchscreen aus.",
                    "Klicken Sie auf Aenderungen uebernehmen.",
                    "Verwenden Sie die physische Maus auf dem Hauptbildschirm.",
                    "Verwenden Sie Touch auf dem Touchscreen.",
                    "Bewegen Sie die physische Maus erneut, um schnell zur zuletzt gespeicherten Position auf dem Hauptbildschirm zurueckzukehren."
                ],
                "Sprache und Diagnose",
                "Die Anwendung verwendet standardmaessig die Systemsprache, aber Sie koennen die Oberflaechensprache ueber den Auswahlschalter oben im Fenster aendern.",
                "Verwenden Sie die Registerkarte Diagnose nur, wenn Sie Protokolle fuer Tests oder Support benoetigen.",
                "Mauszugriff auf den Touchscreen",
                "Auf der Registerkarte Verhalten koennen Sie erlauben oder blockieren, dass die physische Maus den als Touchscreen konfigurierten Monitor betritt."
            ),
            new HelpContent(
                "it",
                "Aiuto di MouseTool",
                "MouseTool mantiene il mouse ancorato allo schermo principale mentre un touchscreen funziona su un altro monitor.",
                "Come usare",
                [
                    "Seleziona correttamente lo schermo principale e lo schermo touchscreen nella scheda Schermi.",
                    "Fai clic su Applica modifiche.",
                    "Usa il mouse fisico sullo schermo principale.",
                    "Usa il tocco sullo schermo touchscreen.",
                    "Muovi di nuovo il mouse fisico per tornare rapidamente all'ultima posizione salvata sullo schermo principale."
                ],
                "Lingua e diagnostica",
                "L'applicazione usa per impostazione predefinita la lingua del sistema, ma puoi cambiare la lingua dell'interfaccia dal selettore nella parte alta della finestra.",
                "Usa la scheda Diagnostica solo quando hai bisogno dei log per test o supporto.",
                "Accesso del mouse allo schermo touchscreen",
                "Nella scheda Comportamento puoi consentire o bloccare l'ingresso del mouse fisico nel monitor configurato come area touchscreen."
            ),
            new HelpContent(
                "ru",
                "Spravka MouseTool",
                "MouseTool uderzhivaet mysh na osnovnom ekrane, poka sensornyy ekran rabotaet na drugom monitore.",
                "Kak ispolzovat",
                [
                    "Pravilno vyberite osnovnoy ekran i sensornyy ekran na vkladke Ekrany.",
                    "Nazhmitie Primenit izmeneniya.",
                    "Ispolzuyte fizicheskuyu mysh na osnovnom ekrane.",
                    "Ispolzuyte kasanie na sensornom ekrane.",
                    "Snova dvignite fizicheskuyu mysh, chtoby bystro vernutsya k posledney sokhranennoy pozitsii na osnovnom ekrane."
                ],
                "Yazyk i diagnostika",
                "Po umolchaniyu prilozhenie ispolzuet sistemnyy yazyk, no vy mozhete izmenit yazyk interfeisa cherez selektor vverhu okna.",
                "Ispolzuyte vkladku Diagnostika tolko togda, kogda vam nuzhny zhurnaly dlya testov ili podderzhki.",
                "Dostup myshi k sensornomu ekranu",
                "Na vkladke Povedenie mozhno razreshit ili zapretit fizicheskoy myshi vhodit na monitor, nastroennyy kak sensornaya oblast."
            )
        ];
    }

    private sealed record HelpContent(
        string LanguageCode,
        string Title,
        string Intro,
        string HowToTitle,
        IReadOnlyList<string> HowToSteps,
        string LanguageSectionTitle,
        string LanguageSectionBody1,
        string LanguageSectionBody2,
        string TouchAccessTitle,
        string TouchAccessBody);
}

