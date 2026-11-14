# Base para un shooter en tercera persona (Unity)

La base de un shooter 3D en tercera persona al estilo Free Fire. Incluye:

- **Movimiento en tercera persona**: WASD o joystick virtual, correr, saltar (con *coyote time*), gravedad y rotación suave.
- **Cámara sobre el hombro**: orbita alrededor del personaje, lo sigue con suavizado, hace zoom al apuntar y no atraviesa paredes.
- **Disparo con raycast**: cargador y reserva de munición, recarga (manual y automática), dispersión, retroceso, trazador, fogonazo, impactos, daño y fuerza física.
- **Enemigos con IA** (`EnemyAI`): patrullan, te detectan con cono de visión y línea de visión, te persiguen, se giran hacia ti y te disparan.
- **Vida del jugador** (`PlayerHealth`): 100 de vida, recibe los disparos de los bots y reinicia la escena al morir.
- **HUD**: munición, recarga, mira, marcador de impacto, barra de vida y flash rojo al recibir daño.
- **Controles táctiles**: joystick, zona para mover la cámara, y botones de disparar, apuntar, saltar, recargar y correr.
- **Generador automático** de la escena de pruebas y de los prefabs.

Requisitos: **Unity 2022.3 LTS o Unity 6**, con cualquier plantilla 3D (Built-in o URP).

---

## Estructura

```
Assets/_Project/
├── Scripts/
│   ├── Player/
│   │   ├── PlayerInputHandler.cs     ← Entrada unificada: teclado y ratón + táctil
│   │   ├── ThirdPersonController.cs  ← Movimiento con CharacterController
│   │   └── PlayerHealth.cs           ← Vida del jugador y reinicio de escena
│   ├── Enemies/
│   │   └── EnemyAI.cs                ← Bot: patrulla, detección, persecución y disparo
│   ├── Camera/
│   │   └── ThirdPersonCamera.cs      ← Cámara orbital sobre el hombro
│   ├── Weapons/
│   │   └── RaycastWeapon.cs          ← Disparo, munición y recarga
│   ├── Combat/
│   │   ├── IDamageable.cs            ← Interfaz para todo lo que recibe daño
│   │   ├── Health.cs                 ← Vida genérica
│   │   └── TargetDummy.cs            ← Diana que parpadea y reaparece
│   └── UI/
│       ├── HUDController.cs          ← Munición, recarga y marcador de impacto
│       ├── VirtualJoystick.cs        ← Joystick táctil
│       ├── TouchLookArea.cs          ← Arrastrar para girar la cámara
│       ├── MobileButton.cs           ← Botones táctiles (mantener o alternar)
│       └── MobileControlsPanel.cs    ← Raíz de los controles móviles
└── Editor/
    └── TPSSceneBuilder.cs            ← Menú que crea la escena y los prefabs
```

Al ejecutar el generador se crean también:

```
Assets/_Project/Materials/            ← Materiales de prueba
Assets/_Project/Prefabs/TPS_PlayerRig.prefab   ← Personaje + arma + cámara
Assets/_Project/Prefabs/TPS_HUD.prefab         ← Canvas del HUD + controles táctiles
Assets/_Project/Prefabs/TPS_Enemy.prefab       ← Bot enemigo listo para arrastrar a la escena
Assets/_Project/Scenes/TPS_TestScene.unity     ← Escena de pruebas
```

> ⚠️ `TPSSceneBuilder.cs` **tiene que estar dentro de una carpeta llamada `Editor`**. Si no, el proyecto no compila al hacer una build.

---

## Cómo probarlo

### Opción A: usar este repositorio como proyecto
1. En Unity Hub, pulsa **Add → Add project from disk** y elige la carpeta del repositorio.
2. Ábrelo con Unity 2022.3 o Unity 6. Unity crea `ProjectSettings/` y `Packages/` automáticamente.

### Opción B: usar un proyecto tuyo
1. Crea un proyecto nuevo con la plantilla **3D** o **3D (URP)**.
2. Copia la carpeta `Assets/_Project` dentro de la carpeta `Assets/` de tu proyecto.

### Después, en ambos casos
3. Espera a que Unity compile y ve al menú **Tools → TPS Shooter → Crear escena de prueba**.
4. Pulsa **Play**. Haz clic en la vista *Game* para capturar el ratón.

## Controles

| Acción | PC | Móvil |
|---|---|---|
| Moverse | WASD o flechas | Joystick izquierdo |
| Correr | Shift izquierdo | Botón CORRER, o empujar el joystick a tope hacia delante |
| Saltar | Espacio | Botón SALTAR |
| Girar la cámara | Ratón | Arrastrar en la mitad derecha de la pantalla |
| Disparar | Clic izquierdo (se puede mantener) | Botón FUEGO (también se puede arrastrar sobre él para apuntar) |
| Apuntar | Clic derecho (mantener) | Botón APUNTAR (pulsar para activar o desactivar) |
| Recargar | R | Botón RECARGAR |
| Soltar el ratón | Esc | — |

**Probar los controles táctiles en el Editor:** selecciona `TPS_PlayerRig/Player` y, en el componente **PlayerInputHandler**, marca **Force Mobile Controls**. En el móvil se activan solos.

---

## Enemigos y vida: montarlo a mano

El generador ya deja todo montado. Si quieres hacerlo en tu propia escena:

**Jugador**
1. Al objeto `Player` (el que tiene el `CharacterController`) añádele **PlayerHealth**.
2. Comprueba que tenga la etiqueta (*Tag*) **Player**.
3. Añade la escena a *File → Build Profiles* (o *Build Settings*) para que se pueda reiniciar en la build. En el Editor se reinicia aunque no esté añadida.

**Bot enemigo**
1. Crea un objeto vacío `Enemy` con el pivote a la altura de los pies, y ponle de hijo una cápsula como cuerpo **sin collider**.
2. Añádele estos componentes:
   - **CharacterController**: sirve de collider para recibir balas y para moverse sin NavMesh. Usa `Height 1.8`, `Center Y 0.9` y `Radius 0.35`.
   - **Health**: su vida. Las balas del jugador le hacen daño.
   - **EnemyAI**.
3. En **EnemyAI**:
   - **Player**: arrastra el objeto `Player` desde la jerarquía. Si lo dejas vacío, lo busca por la etiqueta `Player`.
   - **Muzzle** (opcional): un objeto vacío en la punta del arma.
   - **Tracer** y **Muzzle Flash** (opcionales): un `LineRenderer` y una `Light`.
4. Selecciona el bot para ver sus rangos en la vista *Scene*: amarillo para detección, rojo para disparo y cian para la distancia preferida.

**Opcional: navegación con NavMesh** (para que esquive obstáculos)
1. Instala **AI Navigation** desde *Window → Package Manager*.
2. Añade un objeto con **NavMesh Surface** y pulsa **Bake**.
3. Añade un **NavMeshAgent** al bot. Si está sobre el NavMesh lo usa automáticamente; si no, se mueve con el CharacterController.

**Capas:** el jugador está en `Ignore Raycast`. Por eso `Vision Mask` y `Hit Mask` del bot vienen en *Everything*. Si los cambias, deja incluida la capa del jugador o el bot no podrá verte ni darte.

## Cómo funciona

- **Orden de ejecución.** `PlayerInputHandler` se ejecuta primero (`DefaultExecutionOrder(-100)`). Después, el controlador y el arma leen el input en `Update`, y la cámara en `LateUpdate`. Así todos usan el mismo input en cada frame.
- **Puntería en TPS.** Se lanza un rayo desde el **centro de la pantalla** para saber a qué punto del mundo apunta la mira. Luego se lanza un segundo rayo **desde el cañón** hacia ese punto. Si hay una pared entre el arma y el objetivo, la bala da en la pared.
- **Capa del jugador.** El jugador está en la capa `Ignore Raycast`, así que ni sus disparos ni la cámara lo detectan. Si cambias su capa, quítala de `Hit Mask` (en RaycastWeapon) y de `Collision Mask` (en ThirdPersonCamera).
- **Daño.** Todo lo que tenga un componente que implemente `IDamageable` (por ejemplo `Health`) recibe daño. Los `Rigidbody` además reciben un impulso.
- **Eventos del arma.** `AmmoChanged`, `Fired`, `DryFired`, `ReloadStarted`, `ReloadFinished`, `HitSurface` y `HitDamageable`. Sirven para conectar sonidos, animaciones y UI sin tocar el arma.
- **Input System.** El código funciona con el Input Manager clásico y con el nuevo Input System. Si sólo está activo el nuevo, hay que tener instalado el paquete `com.unity.inputsystem`.

## Ajustes rápidos (en el Inspector)

| Componente | Qué ajustar |
|---|---|
| ThirdPersonController | `Walk/Sprint/Aim Walk Speed`, `Jump Height`, `Gravity` |
| ThirdPersonCamera | `Distance`, `Aim Distance`, `Shoulder Offset`, `Min/Max Pitch`, `Follow Smooth Time`, FOV |
| RaycastWeapon | `Damage`, `Fire Rate`, `Automatic`, `Magazine Size`, `Starting Reserve Ammo`, `Reload Time`, dispersión y retroceso |
| PlayerInputHandler | Sensibilidad de ratón y táctil, `Invert Y`, `Force Mobile Controls` |

## Problemas comunes

- **Error `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...`.** En *Project Settings → Player → Active Input Handling* elige **Both**, o vuelve a generar la escena para que se use el módulo de UI correcto.
- **Los materiales se ven rosas.** Pasa si cambiaste de pipeline (Built-in ↔ URP) después de generar la escena. Vuelve a ejecutar *Tools → TPS Shooter → Crear escena de prueba*.
- **Usar los prefabs en otra escena.** Arrastra `TPS_PlayerRig` y `TPS_HUD` a la escena y crea un *EventSystem* (*GameObject → UI → Event System*). Las referencias entre HUD, arma y controles táctiles se buscan solas.

## Próximos pasos sugeridos

- Animaciones (Animator con un *blend tree* que use `PlanarSpeed`, `IsGrounded` e `IsAiming` de `ThirdPersonController`).
- Varias armas y cambio entre ellas (pasar los datos del arma a un `ScriptableObject`).
- Agacharse, tumbarse y deslizarse.
- Sonido y partículas usando los eventos del arma.
- Oleadas de enemigos y un spawner.
- Animaciones de los bots, y que dejen munición o botiquines (`PlayerHealth.Heal`, `RaycastWeapon.AddReserveAmmo`).
- Multijugador (Netcode for GameObjects o Photon Fusion).
