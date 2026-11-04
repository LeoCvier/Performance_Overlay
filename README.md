# PerformanceOverlay

Ein Windows-Overlay, das wichtige Leistungsdaten deines PCs während des Spielens oder Arbeitens sichtbar macht.

## Was das Overlay anzeigt

- **FPS:** aktuelle durchschnittliche Bildrate sowie höchster und niedrigster Wert der laufenden Sitzung
- **CPU:** Auslastung und Temperatur, sofern dein System die Sensorwerte bereitstellt
- **GPU:** Auslastung und Temperatur, sofern dein System die Sensorwerte bereitstellt
- **RAM:** aktuelle Arbeitsspeicherauslastung

Nicht verfügbare Messwerte werden ausgeblendet. Der grüne Punkt zeigt an, dass Sensordaten verfügbar sind. Bleiben Messwerte länger als fünf Minuten aus, blendet das Overlay sie wieder aus.

## Installation

Lade unter **Releases** die Datei `PerformanceOverlay-Setup-x64.exe` herunter und starte sie. Die Setup-Datei installiert die Anwendung und enthält die benötigten Komponenten einschließlich PresentMon. Du musst **weder das .NET-Runtime/SDK noch PresentMon separat installieren**.

Die Installation fragt über die Windows-Benutzerkontensteuerung nach Administratorrechten. Die Anwendung benötigt erhöhte Rechte für ihre Hardware- und FPS-Erfassung. Das Setup richtet außerdem den automatischen Start bei der Windows-Anmeldung für das installierende Konto ein. Dafür muss dieses Konto Administratorrechte besitzen.

## Verwendung

Nach der Installation startet das Overlay automatisch bei der Anmeldung. Das Overlay bleibt über anderen Fenstern am oberen Rand des Bildschirms. Über das Symbol im Infobereich der Taskleiste erreichst du die Einstellungen:

- **Show overlay / Hide overlay:** Overlay ein- oder ausblenden
- **Overlay position:** oben links, mittig oder rechts platzieren
- **Overlay color:** Akzentfarbe auswählen
- **Configure hotkeys:** Tastenkürzel anpassen
- **Reload everything:** Sensoren und FPS-Erfassung neu starten und die FPS-Minimal- und Maximalwerte zurücksetzen
- **Exit:** Anwendung beenden

Standard-Tastenkürzel:

- `Ctrl + Shift + R`: FPS-Minimum und -Maximum zurücksetzen
- `Ctrl + Alt + Q`: Anwendung beenden
- `Ctrl + Alt + L`: alles neu laden

Falls ein Tastenkürzel bereits von einem anderen Programm verwendet wird, kann es nicht registriert werden. Weise ihm im Taskleistenmenü ein anderes Kürzel zu.

Die gewählte Position, Farbe und Tastenkürzel werden in `%APPDATA%\PerformanceOverlay\settings.json` gespeichert.

## Wie die Messung funktioniert

Das Overlay liest CPU- und GPU-Sensoren über LibreHardwareMonitor aus und ermittelt die RAM-Auslastung über Windows. PresentMon erfasst die Bildrate unterstützter Anwendungen. Die Verfügbarkeit einzelner Sensorwerte hängt von Hardware, Firmware, Treibern und Berechtigungen ab. Deshalb können einzelne Werte auf manchen Systemen fehlen.

Die FPS-Anzeige enthält den aktuellen Durchschnitt sowie Minimum und Maximum seit dem letzten Zurücksetzen oder Neustart der Anwendung. Beim Start der FPS-Erfassung zeigt ein grüner animierter Rahmen an, dass auf die ersten Daten gewartet wird. Ein violetter Rahmen zeigt an, dass gerade **Reload everything** ausgeführt wird.
