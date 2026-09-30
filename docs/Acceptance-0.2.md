# Criterios de aceptación 0.2

## Preparación

1. Compilar la solución.
2. Abrir AutoCAD 2027.
3. Ejecutar `NETLOAD` sobre `CadSemanticAssistant.Plugin.dll`.
4. Mantener la carpeta `Config` junto a la DLL:

```text
CadSemanticAssistant.Plugin.dll
Config/
└── semantic-mapping.json
```

## Tabla de aceptación

| Caso | Validación | Resultado esperado |
|---|---|---|
| `CSA_READBLOCKS` sigue funcionando | Ejecutar `CSA_READBLOCKS` | Se muestran los bloques de ModelSpace |
| `CSA_READENTITIES` funciona | Ejecutar `CSA_READENTITIES` | Se muestran entidades semánticas sin error de configuración |
| `RACK_*` → Rack | Usar un bloque `RACK_SC2_A` | La entidad es `Rack` |
| `MACHINE_*` → Machine | Usar un bloque `MACHINE_001` | La entidad es `Machine` |
| Configuración externa JSON | Modificar `Config/semantic-mapping.json` | Las reglas se cargan desde el archivo, sin recompilar el mapper |
| Atributos normalizados | Usar etiquetas con distinta capitalización o espacios | Se encuentran correctamente |
| `HEIGHT` → `height` | Configurar `SourceAttribute: HEIGHT` y `TargetProperty: height` | La propiedad se llama `height` |
| String → double | Valor `12000` con `DataType: Double` | El valor es `12000d` |
| String → integer | Valor `40` con `DataType: Integer` | El valor es `40` |
| Unidades conservadas | Configurar `Unit: mm` | La unidad aparece como `mm` |
| Bloque desconocido → Unknown | Usar un nombre sin regla | Tipo `Unknown` y warning |
| Error de conversión → warning | Usar `HEIGHT: not-a-number` | Se genera warning y no se agrega la propiedad inválida |
| Bloque dinámico usa nombre efectivo | Bloque dinámico con nombre efectivo `RACK_*` | Se aplica la regla al nombre efectivo |
| Mapper testeable sin AutoCAD | Ejecutar tests de `RuleBasedCadSemanticMapper` | No requiere DLL ni proceso de AutoCAD |
| Tests unitarios funcionando | Ejecutar `dotnet test --no-restore` | Todos los tests pasan |

## Validación automatizada

```powershell
dotnet build
dotnet test --no-restore
```

La suite cubre las reglas Rack/Machine, configuración JSON externa, normalización de atributos, conversiones, unidades, warnings, bloques dinámicos, bloques desconocidos y el aislamiento del mapper respecto a AutoCAD.
