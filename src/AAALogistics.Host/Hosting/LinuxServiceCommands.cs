namespace AAALogistics.Host.Hosting;

internal static class LinuxServiceCommands
{
    public const string Install =
        """
        Instalación del servicio en Linux:

        1. Publique el servidor:
           dotnet publish src/AAALogistics.Host -c Release -o ./artifacts/server

        2. Como root:
           sudo ./deploy/install-linux.sh ./artifacts/server

        El script copia el binario a /opt/aaalogistics, instala
        deploy/aaalogistics.service y lo habilita con systemd.
        El proceso mantiene Data/aaa-host.lock y rechaza una segunda instancia.

        Este comando no copia ficheros ni llama a systemctl: solo imprime los pasos.
        """;

    public const string Uninstall =
        """
        Desinstalación del servicio en Linux:

          sudo systemctl disable --now aaalogistics.service
          sudo rm -f /etc/systemd/system/aaalogistics.service
          sudo systemctl daemon-reload

        Conserve o borre /opt/aaalogistics según quiera guardar Data/
        (claves, correos y copias). No borre ese directorio a ciegas.
        """;
}
