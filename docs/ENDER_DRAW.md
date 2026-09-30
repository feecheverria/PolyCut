# End3r Draw

Adaptación de PolyCut para una Ender 3 Pro con herramientas intercambiables. El primer uso es dibujar portadas con lápiz. El motor original de corte con compensación de cuchilla permanece disponible para otro montaje y otro perfil.

## Abrir localmente

Haz doble clic en `Iniciar End3r Draw.cmd`. El iniciador utiliza el SDK .NET 10 instalado junto a este proyecto en `../.tools/dotnet`, o el SDK .NET 10 del sistema. Compila Release si aún no existe una compilación.

Para instalar el proyecto desde GitHub:

```powershell
git clone --recurse-submodules https://github.com/feecheverria/PolyCut.git
cd PolyCut
git switch feat/ender-draw-pen-workspace
git submodule update --init --recursive
dotnet build PolyCut/PolyCut.vbproj -c Release
```

Se necesita **Windows y el SDK .NET 10**. LazyTranslate se referencia desde el submódulo oficial fijado por Git, porque su paquete principal todavía no se encuentra en NuGet público. El resto de paquetes se restaura desde NuGet.org. La instalación original de PolyCut conserva sus datos: esta variante guarda perfiles en `.End3rDrawData`, junto al ejecutable.

## Flujo inicial

1. En **Home → Cuaderno**, elige el perfil **Ender 3 Pro + Pen Holder**.
2. Abre **Calibrar impresora / herramienta** y registra los valores medidos del montaje.
3. Elige **Custom 13 x 9 cm** (90 × 130 mm en vertical), **A5** (148 × 210 mm) o introduce las dimensiones reales de tu Traveler. **Aplicar formato** cambia las dimensiones; **Girar 90°** intercambia ancho y alto. Traveler no presupone un tamaño universal.
4. Ubica el papel con X/Y desde arriba a la izquierda. El lienzo muestra cama, área segura verde y papel azul claro. Estos contornos son referencias y no forman parte del dibujo exportado.
5. Importa SVG o una imagen mediante las herramientas originales de PolyCut. Ajusta el dibujo en Elements/Properties. El dibujo no se mueve automáticamente al mover el papel: hay que mantener el diseño dentro del contorno.
6. Elige Draw en Tool Config y genera G-code. Preview muestra el recorrido real de la punta. Después guarda el archivo desde Export.

A5 puede exceder el área alcanzable con tu soporte. Elegir el formato no garantiza que toda la hoja sea dibujable; solo se exportan trabajos cuyas trayectorias quepan en el papel, el área segura y el alcance compensado de la boquilla.

## Calibración y coordenadas

La cama física nominal es de **235 × 235 mm**. El área inicial de 150 × 150 mm situada en (30, 30), los límites de boquilla 0–220 mm, el Z máximo de 250 mm y las alturas Z 0/5 mm son **referencias editables sin verificar**. La exportación empieza bloqueada. No marcan el alcance real del montaje.

Mide y registra:

- Offset físico X/Y de la punta respecto de la boquilla.
- Límites mínimos y máximos reales de movimiento de la boquilla.
- Rectángulo seguro de la punta, descontando bordes, pinzas y limitaciones del montaje.
- Z de contacto sobre el material, Z de traslado con separación suficiente y Z máximo disponible.

**Signo:** offset físico = punta − boquilla, con X hacia la derecha e Y hacia el fondo. Un lápiz a la izquierda tiene offset X negativo. El G-code calcula boquilla = punta − offset físico. Por ejemplo, un offset físico X de −25 mm convierte una punta deseada en X=50 mm en una boquilla en X=75 mm. Este ejemplo no es una medida de tu soporte.

Home usa origen arriba a la izquierda. El área segura y los límites físicos se ingresan desde abajo a la izquierda. La conversión es `tipY = bedHeight − canvasY`; Preview devuelve los movimientos compensados a coordenadas de la punta.

Marca **Medí offsets, límites y Z para esta herramienta** cuando esos datos estén registrados. Genera el **Cuadrado** o la **Cuadrícula** central, de hasta 40 mm, revisa Preview y prueba sobre material de descarte. Estos patrones no se restringen al papel, pero sí mantienen la validación del área segura, los límites compensados y Z. Ajusta el perfil a partir de las mediciones. La comprobación del software no sustituye esta prueba física.

Haz homing antes de montar el lápiz y conserva ese sistema de coordenadas. Los archivos no hacen homing automático ni habilitan extrusión; apagan hotend y cama, seleccionan milímetros y posiciones absolutas, elevan Z antes del primer movimiento XY y terminan con la punta levantada. El G-code personalizado del upstream queda deshabilitado en esta etapa para mantener este contrato.

## Cortador

**Preparar perfil de cortador** crea un perfil separado y pendiente de calibración. Conserva el modo Cut original, sus arcos de compensación, overcut y pasadas. El perfil guarda sus propios offsets, alturas Z y límites; el modo debe coincidir con la herramienta del perfil. El perfil de lápiz usa una sola pasada sin descenso Z. El montaje de cuchilla y su calibración física quedan para una siguiente etapa.

## Validación reproducible

```powershell
# Solo comprobaciones del generador, sin ventanas ni impresora
.\scripts\Start-End3rDraw.ps1 -Check -NoLaunch

# Arranque WPF, importación SVG, Preview, guardado y bloqueos con datos simulados
.\scripts\Start-End3rDraw.ps1 -UiCheck -NoLaunch
```

La segunda prueba abre y cierra la aplicación y guarda imágenes en `artifacts/smoke`. Usa perfiles aislados. No envía comandos a ninguna impresora. Se verificaron 55 casos: offsets, Z, límites, comandos sin extrusión/calefacción, patrones, perfiles, formatos, SVG, archivos desactualizados y persistencia.

Si cambia el diseño, el papel, los parámetros de dibujo o el perfil, hay que regenerar antes de guardar, copiar o subir G-code. Cambiar únicamente el destino de exportación no invalida el recorrido. Los calentadores siguen apagados en la previsualización de caja enviada a Moonraker. Auto-start está desactivado inicialmente; la conexión a una impresora no se ha probado.

Compilación Release y flujo local validados. Permanecen avisos heredados del upstream sobre APIs obsoletas y algunos métodos async. La vectorización raster existente se conserva; esta validación inicial cubre SVG, no una prueba exhaustiva de todos los formatos raster.

## Próximo trabajo físico

Medir el soporte real, probar el cuadrado y la cuadrícula, registrar el área útil definitiva y dibujar una portada de descarte de 13 × 9 cm. Luego se pueden agregar centrado/ajuste al papel, márgenes, presets de diseños y calibración específica del cortador.

Se conserva la licencia y la atribución del proyecto PolyCut original.
