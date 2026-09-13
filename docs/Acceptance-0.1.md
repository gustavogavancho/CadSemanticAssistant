# Criterios de aceptación 0.1

## Preparación

1. Compilar la solución en configuración `Debug`.
2. Abrir AutoCAD 2027.
3. Ejecutar `NETLOAD` y seleccionar:

```text
CadSemanticAssistant.Plugin/bin/Debug/net10.0/CadSemanticAssistant.Plugin.dll
```

## Smoke test del plugin

| Caso | Pasos | Resultado esperado |
|---|---|---|
| Plugin carga con NETLOAD | Ejecutar `NETLOAD` y seleccionar la DLL | AutoCAD carga la DLL sin error y muestra `CAD Semantic Assistant loaded.` |
| `CSA_PING` funciona | Ejecutar `CSA_PING` en la línea de comandos | Aparece `CAD Semantic Assistant is running.` |

## Smoke test de lectura

Preparar un DWG que contenga en `ModelSpace`:

- Un bloque normal con Handle, nombre y Layer conocidos.
- Un bloque dinámico con nombre efectivo distinto al nombre anónimo.
- Un bloque con atributos.
- Una referencia externa (XRef).

Ejecutar `CSA_READBLOCKS` y comprobar:

- Se leen únicamente los `BlockReference` de `ModelSpace`.
- Se muestra el Handle.
- Se muestra el nombre del bloque.
- Para el bloque dinámico se muestra el nombre efectivo y, cuando difiere, el nombre bruto.
- Se muestra el Layer.
- Se muestran las coordenadas X, Y y Z.
- Se muestra la rotación en grados.
- Se muestran las etiquetas y valores de los atributos.
- La XRef aparece con `XRef: True`.

Abrir después un DWG sin referencias de bloque y ejecutar `CSA_READBLOCKS`. El comando debe mostrar `Found 0 block references.` sin lanzar una excepción.

## Validaciones automatizadas

Desde la raíz del repositorio:

```powershell
dotnet build
dotnet test --no-restore
```

Las pruebas comprueban el contrato de bloques, el caso sin bloques, la igualdad de `CadPoint` y que el ensamblado Domain no referencia Autodesk.
