# Базовая линия перед фиксами 1-5 (2026-11-03 15:22, прод e3acef1b)

iPhone 11 iOS 16.6 Safari, https://pixel-swarm.xyz/?autorun=1, сессия fe34c2cb-be96-4f8e-9850-0eee0f4edef2, 5.5 мин забега. Зависаний GPU нет (dt.max 583 мс один раз). Упор в CPU главного потока, растёт с частицами/искрами/светом.

```
session fe34c2cb-be96-4f8e-9850-0eee0f4edef2 status=live tags=run started=2026-11-03T10:22:27.4031449Z closed=None startup=18c310f5-d562-4313-89f0-11b605d1b81f samples=326 hidden=0
device platform=webgl build=1.0 browser=Safari 16 os=iOS 16 gpu='Apple GPU' cores=4 memoryGB=- screen=414x896@2
not_reported frame.gpu render.textures web.js.used web.js.total web.js.limit frame.gpu.query
metric                     unit   n    avg      p50      p95      min     max      growth
net.meta.rtt               ms     145  258.7    250      333      164     600      -
net.meta.bytes_in          bytes  326  0.0MB    0.0MB    0.0MB    0.0MB   0.0MB    -0.0MB
net.meta.bytes_out         bytes  326  0.0MB    0.0MB    0.0MB    0.0MB   0.0MB    -0.0MB
web.gl.context_lost        -      326  0        0        0        0       0        -
web.gl.errors              count  326  0        0        0        0       1        -
web.gl.errors_ms           ms     326  0.1      0        1        0       1        -
game.ui.canvases           count  326  4.8      5        5        0       5        -
game.ui.graphics           count  326  21.2     18       22       0       97       -
game.ui.graphics.culled    count  326  0        0        0        0       0        -
game.ui.texts              count  326  2.5      2        2        0       14       -
game.ui.fonts              count  326  2.9      3        3        1       3        -
game.ui.font.atlases       count  326  2.9      3        3        1       3        -
game.ui.font.atlas_bytes   bytes  326  0.4MB    0.4MB    0.4MB    0.2MB   0.4MB    0.1MB
game.ui.font.glyphs        count  326  499.6    510      510      170     510      -
frame.fps                  fps    326  35       32.9     53.1     14.9    60       -
frame.dt.avg               ms     326  32.3     30.4     55.1     16.7    66.9     -
frame.dt.p50               ms     326  31       30       54       16      61       -
frame.dt.p95               ms     326  42.5     39       75       17      222      -
frame.dt.max               ms     326  59.7     55       82       17      583      -
frame.over33               count  326  9.6      6        23       0       27       -
frame.over50               count  326  2.5      1        12       0       16       -
frame.cpu                  ms     326  32.3     30.3     54.8     16.6    65.4     -
frame.cpu.main             ms     326  27.8     27       51.1     2.9     61.6     -
memory.allocated           bytes  326  71.6MB   72.3MB   74.8MB   29.4MB  76.5MB   44.9MB
memory.reserved            bytes  326  83.1MB   84.6MB   87.2MB   35.7MB  88.9MB   50.9MB
memory.mono.used           bytes  326  16.0MB   15.5MB   24.0MB   3.4MB   26.5MB   18.8MB
memory.mono.heap           bytes  326  20.9MB   22.3MB   30.2MB   3.9MB   30.2MB   26.3MB
memory.textures            bytes  326  29.4MB   29.5MB   29.6MB   2.6MB   29.6MB   26.9MB
memory.texture_count       count  326  525.8    531      536      274     536      -
gc.collections             count  326  0.1      0        1        0       1        -
frame.phase.early          ms     326  0.3      0.2      0.3      0       19.7     -
frame.phase.early.max      ms     326  3.9      1        1        0       318      -
frame.phase.fixed          ms     326  0.3      0.3      0.5      0       1        -
frame.phase.fixed.max      ms     326  1.2      1        1        1       30       -
frame.phase.pre            ms     326  0.1      0.1      0.2      0       0.4      -
frame.phase.pre.max        ms     326  1        1        1        0       7        -
frame.phase.update         ms     326  12.7     11.9     26.8     0.2     35       -
frame.phase.update.max     ms     326  22.7     19       45       1       542      -
frame.phase.prelate        ms     326  0        0        0.1      0       0.5      -
frame.phase.prelate.max    ms     326  0.8      1        1        0       6        -
frame.phase.postlate       ms     326  14.3     13.6     24.7     0.9     30.6     -
frame.phase.postlate.max   ms     326  31.8     23       59       2       316      -
frame.fixed_steps          count  326  1.6      1.5      2.8      0.8     3.4      -
frame.fixed_steps.max      count  326  3.1      3        4        1       17       -
frame.spikes               count  326  0        0        0        0       1        -
gc.allocated               bytes  326  0.3MB    0.3MB    0.5MB    0.0MB   8.2MB    0.0MB
settings.quality           -      326  0        0        0        0       0        -
settings.vsync             count  326  0        0        0        0       0        -
settings.target_fps        fps    326  -1       -1       -1       -1      -1       -
settings.time_scale        -      326  1        1        1        1       1        -
settings.msaa              count  326  0        0        0        0       0        -
render.width               px     326  896      896      896      896     896      -
render.height              px     326  364      364      364      364     364      -
render.pixels              px     326  326144   326144   326144   326144  326144   -
render.canvas_width        px     326  896      896      896      896     896      -
render.canvas_height       px     326  364      364      364      364     364      -
render.dpr                 -      326  1        1        1        1       1        -
web.wasm.heap              bytes  326  135.2MB  138.0MB  165.6MB  55.4MB  165.6MB  110.2MB
web.raf.dt.avg             ms     326  32.3     30.4     55.7     16.6    67.7     -
web.raf.dt.max             ms     326  60       55       83       17      583      -
web.gl.version             -      326  2        2        2        2       2        -
web.gl.max_texture_size    -      326  16384    16384    16384    16384   16384    -
web.gl.antialias           -      326  0        0        0        0       0        -
web.gl.preserve_buffer     -      326  0        0        0        0       0        -
web.gl.power_preference    -      326  1        1        1        1       1        -
web.gl.timer_query         -      326  0        0        0        0       0        -
web.gl.shader_compiles     count  326  0.1      0        0        0       22       -
web.gl.shader_compile_ms   ms     326  0.4      0        0        0       83       -
web.gl.program_links       count  326  0.1      0        0        0       11       -
web.gl.program_link_ms     ms     326  0.1      0        0        0       10       -
web.gl.buffer_bytes        bytes  326  35.3MB   35.4MB   48.4MB   0.0MB   54.5MB   27.6MB
web.gl.buffer_data_calls   count  326  384.1    398      623      0       1309     -
web.gl.texture_bytes       bytes  326  0.1MB    0.0MB    0.0MB    0.0MB   23.4MB   0.0MB
web.gl.texture_uploads     count  326  2.7      2        3        0       134      -
render.draw_calls          count  324  90.8     76       198      14      307      -
render.setpass             count  324  116.4    101      223      41      331      -
render.triangles           count  324  18661    17025    34400    2086    60707    -
render.vertices            count  324  36520    32952    67850    4184    119915   -
render.render_textures     count  324  14       14       14       14      14       -
game.swarm.bodies          count  315  209.3    200      381      0       464      -
game.sparks                count  315  47.8     20       176      0       236      -
game.projectiles           count  315  17.2     11       56       0       87       -
game.particles             count  315  387.1    175      1607     0       3797     -
game.lights.transient      count  315  3        1        16       0       62       -
game.lights.enabled        count  315  66.8     37       196      15      301      -
game.players               count  315  1        1        1        1       1        -
game.swarm.step            ms     315  4        3.8      8        0.6     12.2     -
game.swarm.step.max        ms     315  9.3      10       15       1       49       -
game.swarm.view            ms     315  3.5      3.5      6.8      0       8.8      -
game.swarm.view.max        ms     315  4.5      4        9        1       13       -
game.swarm.field.builds    count  307  3.1      4        4        0       4        -
game.swarm.field           ms     286  5.6      5.7      7        2       16       -
game.swarm.field.max       ms     286  6.1      6        7        2       34       -
game.particles.update      ms     308  1.9      1.1      6.5      0       15       -
game.particles.update.max  ms     308  4.3      2        15       0       88       -
game.sparks.update         ms     308  0.3      0.1      0.8      0       1.6      -
game.sparks.update.max     ms     308  1.2      1        2        0       9        -
game.lights.update         ms     308  0        0        0.2      0       0.5      -
game.lights.update.max     ms     308  0.5      0        2        0       4        -
game.lights.pass           ms     315  0.3      0.2      0.6      0       0.9      -
game.lights.pass.max       ms     315  1        1        1        1       1        -
game.stage                 -      315  1        1        1        1       1        -
game.phase                 -      315  0.1      0        0        0       5        -
game.level                 -      315  6.4      7        10       1       10       -
game.session.owner         -      315  1        1        1        1       1        -
trend (over*, gc.collections, the per-sample counters: sum; *.max: max; bytes: last value; the rest: average)
t_s                        1-57      57-112    112-168   168-224   224-279   279-335
samples                    55        55        55        55        54        55
frame.cpu                  21.3      21.8      26.4      33.7      46        44
frame.cpu.main             15        14.5      23.2      30.5      42.5      40.4
frame.dt.avg               21.4      21.8      26.4      33.8      46.1      43.9
frame.dt.max               583       74        77        72        149       172
frame.dt.p50               19.1      21        25.3      32.8      44.5      42.5
frame.dt.p95               32.4      26.9      33.7      43.4      59        58.4
frame.fixed_steps          1.1       1.1       1.3       1.7       2.3       2.2
frame.fixed_steps.max      17        4         4         4         8         9
frame.fps                  48        46        38.7      29.8      22.3      26.4
frame.over33               78        86        203       708       1111      956
frame.over50               61        83        21        22        299       321
frame.phase.early          1         0.1       0.2       0.2       0.2       0.2
frame.phase.early.max      318       1         1         1         3         1
frame.phase.fixed          0.3       0.3       0.3       0.3       0.4       0.3
frame.phase.fixed.max      30        1         1         1         1         1
frame.phase.postlate       9.5       9.3       13        15        19.6      18.9
frame.phase.postlate.max   316       62        64        33        88        54
frame.phase.pre            0.1       0.1       0.1       0.1       0.1       0.1
frame.phase.pre.max        7         1         1         1         1         1
frame.phase.prelate        0.1       0         0         0.1       0.1       0.1
frame.phase.prelate.max    6         3         4         5         4         5
frame.phase.update         3.9       4.6       9.5       14.8      22.1      20.7
frame.phase.update.max     542       17        31        36        65        113
frame.spikes               5         0         0         0         0         0
game.level                 2.1       4.3       6.3       7.5       8.4       9.3
game.lights.enabled        17.7      34.9      32.3      33.6      111.1     165.1
game.lights.pass           0.2       0.2       0.2       0.2       0.5       0.5
game.lights.pass.max       1         1         1         1         1         1
game.lights.transient      0.2       0.4       1.4       1         7.1       7.3
game.lights.update         0         0         0         0         0.1       0.1
game.lights.update.max     1         1         1         1         3         4
game.particles             29.4      70.8      206.9     345       613.3     970.8
game.particles.update      0.2       0.4       1.1       1.9       3         4.1
game.particles.update.max  1         2         13        11        30        88
game.phase                 0         0         0         0         0         0.7
game.players               1         1         1         1         1         1
game.projectiles           0.2       0.9       8.2       21.6      34        34.3
game.session.owner         1         1         1         1         1         1
game.sparks                1.7       18.3      14.9      16.3      87.6      142.9
game.sparks.update         0         0.1       0.1       0.2       0.5       0.7
game.sparks.update.max     7         1         2         3         5         9
game.stage                 1         1         1         1         1         1
game.swarm.bodies          35.3      88.1      165.7     251.3     354.4     317.6
game.swarm.field           5.8       3.8       5.6       6.1       6.4       5.9
game.swarm.field.builds    3.2       3.1       2.9       2.7       3.5       3.3
game.swarm.field.max       34        6         8         8         10        7
game.swarm.step            1.2       1.3       2.8       4.6       7.6       5.9
game.swarm.step.max        49        8         12        13        21        19
game.swarm.view            0.6       1         2.6       4.3       6.3       5.3
game.swarm.view.max        3         3         5         8         13        10
game.ui.canvases           3.9       5         5         5         5         5
game.ui.font.atlas_bytes   0.4MB     0.4MB     0.4MB     0.4MB     0.4MB     0.4MB
game.ui.font.atlases       2.6       3         3         3         3         3
game.ui.font.glyphs        448.2     510       510       510       510       510
game.ui.fonts              2.6       3         3         3         3         3
game.ui.graphics           18.4      22.3      23.7      20.9      20.9      20.9
game.ui.graphics.culled    0         0         0         0         0         0
game.ui.texts              2.2       2.7       2.9       2.4       2.4       2.4
gc.allocated               14.0MB    7.9MB     16.1MB    18.5MB    23.4MB    24.5MB
gc.collections             5         3         4         4         5         5
memory.allocated           69.7MB    71.3MB    72.2MB    72.9MB    74.6MB    74.2MB
memory.mono.heap           16.6MB    16.6MB    22.3MB    22.3MB    30.2MB    30.2MB
memory.mono.used           11.5MB    12.1MB    16.2MB    16.9MB    19.8MB    22.5MB
memory.reserved            79.3MB    83.2MB    84.6MB    85.3MB    87.0MB    86.6MB
memory.texture_count       507.1     523       526.8     531       531.3     536
memory.textures            29.5MB    29.5MB    29.5MB    29.5MB    29.6MB    29.6MB
net.meta.bytes_in          0.0MB     0.0MB     0.0MB     0.0MB     0.0MB     0.0MB
net.meta.bytes_out         0.1MB     0.0MB     0.0MB     0.0MB     0.0MB     0.0MB
net.meta.rtt               246.3     240.2     259.1     254.5     271       286.8
render.canvas_height       364       364       364       364       364       364
render.canvas_width        896       896       896       896       896       896
render.dpr                 1         1         1         1         1         1
render.draw_calls          47.6      64        77.6      78        129.6     145
render.height              364       364       364       364       364       364
render.pixels              326144    326144    326144    326144    326144    326144
render.render_textures     14        14        14        14        14        14
render.setpass             75.3      89.4      102.7     103.7     154.7     169.4
render.triangles           12527     13205     18119     18871     23436     25129
render.vertices            24892     25953     35246     36598     45804     49337
render.width               896       896       896       896       896       896
settings.msaa              0         0         0         0         0         0
settings.quality           0         0         0         0         0         0
settings.target_fps        -1        -1        -1        -1        -1        -1
settings.time_scale        1         1         1         1         1         1
settings.vsync             0         0         0         0         0         0
web.gl.antialias           0         0         0         0         0         0
web.gl.buffer_bytes        1720.8MB  1885.6MB  2234.6MB  1940.6MB  1774.9MB  2027.5MB
web.gl.buffer_data_calls   12242     23047     27242     25522     20227     17524
web.gl.context_lost        0         0         0         0         0         0
web.gl.errors              2         0         0         0         0         0
web.gl.errors_ms           0.1       0.1       0         0.1       0.1       0
web.gl.max_texture_size    16384     16384     16384     16384     16384     16384
web.gl.power_preference    1         1         1         1         1         1
web.gl.preserve_buffer     0         0         0         0         0         0
web.gl.program_link_ms     31        0         0         0         0         0
web.gl.program_links       19        0         0         0         0         0
web.gl.shader_compile_ms   138       0         0         0         0         0
web.gl.shader_compiles     38        0         0         0         0         0
web.gl.texture_bytes       27.0MB    0.1MB     0.1MB     0.1MB     0.1MB     0.1MB
web.gl.texture_uploads     384       91        113       100       107       84
web.gl.timer_query         0         0         0         0         0         0
web.gl.version             2         2         2         2         2         2
web.raf.dt.avg             21.4      21.8      26.4      33.8      46.1      43.9
web.raf.dt.max             583       73        77        68        149       172
web.wasm.heap              115.0MB   138.0MB   138.0MB   138.0MB   138.0MB   165.6MB
marks 1.7s=frame.spike 1.7s=frame.spike.early 2.0s=webgl.error.invalid_operation 2.4s=frame.spike 2.4s=frame.spike.postlate 3.1s=webgl.error.invalid_operation 3.9s=frame.spike 3.9s=frame.spike.early 6.4s=frame.spike 6.4s=frame.spike.update 8.6s=frame.spike 8.6s=frame.spike.early 11.9s=run.started 11.9s=game.stage.1.fight 22.5s=game.upgrade.pause 24.1s=game.upgrade.resume 44.3s=game.upgrade.pause 45.9s=game.upgrade.resume 65.2s=game.upgrade.pause 66.7s=game.upgrade.resume 89.1s=game.upgrade.pause 90.6s=game.upgrade.resume 119.6s=game.upgrade.pause 121.2s=game.upgrade.resume 147.2s=game.upgrade.pause 148.8s=game.upgrade.resume 193.9s=game.upgrade.pause 195.5s=game.upgrade.resume 255.9s=game.upgrade.pause 257.5s=game.upgrade.resume 287.8s=game.upgrade.pause 289.4s=game.upgrade.resume 317.0s=game.upgrade.pause 318.7s=game.upgrade.resume 327.7s=game.stage.1.summon
loads started=7 ended=7 failed=0 slow=0 unfinished=0 (a load begun before the session has no start)
  longest 3.76s Group: MusicRunAudio, 2.40s Group: SfxAudio, 2.29s Group: GamePlayPrefabs, 2.06s Scene: Game, 1.08s Group: SwarmAssets
errors groups=1
e68a7e29213a70df error count=2 rows=1 first=3.1s last=3.1s WebGL error INVALID_OPERATION
```
