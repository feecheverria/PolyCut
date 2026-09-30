# End3r Draw

Aplicación de dibujo con lápiz para una Ender 3 Pro, basada en un fork de PolyCut. Adaptar el programa existente y su interfaz antes que reconstruirlo desde cero.

## Estado actual

El soporte, el lápiz y PolyCut ya permitieron dibujar correctamente en la impresora. El prototipo físico funciona. Se creó el fork https://github.com/feecheverria/PolyCut y se clonó en `PolyCut/`, con la primera adaptación en la rama `feat/ender-draw-pen-workspace`.

La aplicación compila y se validó localmente con 55 comprobaciones, incluido arranque WPF, importación SVG, Preview y persistencia. Ya incluye formatos A5 y 13 × 9 cm, papel en el lienzo, perfiles de lápiz/cortador separados, offsets físicos, límites, patrones de calibración y bloqueos de generación/exportación. Los datos de prueba fueron simulados; falta calibrar el montaje real.

Para abrir: `Iniciar End3r Draw.cmd`. Instrucciones completas: `PolyCut/docs/ENDER_DRAW.md`.

## Alcance del MVP

- Mantener las pestañas Home, Preview y Export; ocultar Monitor.
- Incluir un perfil inicial «Ender 3 Pro + Pen Holder».
- Registrar el desplazamiento físico X/Y de la punta del lápiz respecto de la boquilla. El lápiz está hacia la izquierda; los valores exactos se deben medir y calibrar.
- Distinguir la cama física nominal de 235 × 235 mm del área dibujable y segura, que será menor y calibrable según el montaje y los límites reales de movimiento.
- Mostrar en Home la cama, el área segura y la posición/tamaño del papel o cuaderno, junto con el diseño que se quiere dibujar.
- Aplicar la compensación del lápiz de forma coherente en posicionamiento, preview y exportación. Definir y documentar el signo de los offsets durante la implementación; no asumir medidas ni coordenadas útiles todavía.
- Bloquear la generación de G-code si cualquier trayectoria de dibujo queda fuera del área segura, indicando qué debe corregirse.
- Generar G-code de calibración para un borde/cuadrado y una matriz de puntos o cuadrícula. Comenzar dentro de una zona conservadora y ajustar el área segura con pruebas físicas.
- Subir y bajar el lápiz mediante Z, con alturas de contacto y desplazamiento calibrables.
- Forzar hotend y cama caliente apagados en el G-code generado, sin extrusión.

## Flujo inicial

1. Importar SVG o imagen, aprovechando la vectorización existente de PolyCut.
2. Elegir y ubicar el papel o cuaderno dentro del área segura.
3. Posicionar y escalar el dibujo.
4. Revisar el recorrido en Preview.
5. Validar límites y exportar el archivo G-code.

## Próximo paso

Medir el offset de la punta, los límites de movimiento y las alturas Z del montaje real. Configurar y confirmar esos datos, probar el cuadrado/cuadrícula sobre descarte y validar una primera portada de 13 × 9 cm. El cortador conserva el motor de PolyCut pero necesita un montaje y una calibración independientes. Las referencias iniciales del perfil no representan medidas verificadas de la impresora.
