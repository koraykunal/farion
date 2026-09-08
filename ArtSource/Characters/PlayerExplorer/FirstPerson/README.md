# PlayerExplorer — first-person arms

> **Kullanılmayan çalışma:** Bu ayrı kol yaklaşımı kullanıcı tarafından reddedildi; oyuna import edilmedi veya bağlanmadı. Aşağıdaki paket eski denemedir, güncel entegrasyon talimatı değildir. Güncel uygulama mevcut `SM_PlayerExplorer_A.fbx` tam bedenini ve mevcut Animator'ı kullanır; kamera `Head` konumunu izler, bakış rotasyonu oyuncuda kalır. Bu klasördeki FBX'i mevcut karakterin yerine geçirmeyin.

## Dosyalar

- `PlayerExplorer_FPArms.blend`: düzenlenebilir çalışma. `PlayerExplorer_FP` sahnesi FP kolları; `Scene` orijinal karakterdir.
- `PlayerExplorer_SourceBackup.blend`: işlem öncesi Blender sahnesi.
- `SM_PlayerExplorer_FPArms_A.fbx`: yalnızca FP iskeleti ve iki kol; 3 saniyelik idle animasyonu dahil.
- `check_fp_source.py`: Blender içinde çalıştırılabilecek kaynak kontrolü; kullanıcıya bırakıldı, çalıştırılmadı.

## Hazırlanan model

İki kol toplam 3.427 vertex / 6.788 üçgen; 54 kemik. Orijinal UV'ler, parmak ağırlıkları ve kol twist kemikleri korundu. FP kopyasında kullanılmayan yüz/bacak kemikleri ve boş `V_None` shape key kaldırıldı. Orijinal FBX, `.meta` ve oyuncu prefabları değiştirilmedi.

Ölçek metredir, obje dönüşümleri uygulanmıştır (scale 1). Kamera/pivot orijini `(0, 0, 0)`; Blender'da ileri `-Y`, yukarı `+Z`; FBX Unity için `+Z` ileri / `+Y` yukarı dönüşümüyle dışa aktarıldı. Bu, gövde FBX'inin yerine geçirilecek bir Humanoid model değil, bağımsız Generic viewmodel'dir.

FP kamera: **60 derece dikey FOV, 16:9**. Oyun sahnesinin yazılmış kamera değeri esas alındı; kullanıcının kayıtlı FOV tercihi farklı olabilir. `FP_Camera` lensini değiştirerek kadraj düzenlenebilir. Omuz kesikleri bilerek açık ve kamera kadrajı dışında. Omuz yerleşimi/kol duruşu rest pose değiştirilerek değil `AN_FP_Idle` içindeki pozla düzenlendi. Kemik uzunlukları değiştirilmedi.

`AN_FP_Idle`: 30 fps, frame 1–91, 3 saniye; hafif nefes hareketi, eşleşen başlangıç/bitiş pozu. Yürüme, koşma, etkileşim, silah ve el IK animasyonları bu pakette yoktur. Aşağı bakarken görülecek gövde/bacak modeli de bu kol paketine dahil değildir.

## Unity'ye alma

1. Yeni FBX'i `Assets/Project/Art/Models/Characters/PlayerExplorer/FirstPerson/` altına kopyala. Eski gövde FBX'ini silme/değiştirme. `.blend` dosyalarını `ArtSource` altında bırak.
2. Model: Scale Factor `1`, Convert Units açık. Rig: **Generic**, Avatar Definition `Create From This Model`; dünya karakterinin Humanoid Avatar'ını atama. Optimize Game Objects başlangıçta kapalı kalsın.
3. Animation: Import Animation açık. Tek gelen take'i `AN_FP_Idle` olarak adlandır, tam aralığı koru ve **Loop Time** aç. Root motion kapalı; Animator'da Apply Root Motion kapalı.
4. Materials remap: `MAT_PlayerExplorer_Suit_White` ve `MAT_PlayerExplorer_HardSurface_White` isimlerini projedeki aynı adlı mevcut materyallere bağla. Blender önizlemesi aynı beyaz kostümün mevcut base color, roughness, metallic ve OpenGL normal dokularını kullanır; dokular FBX'e gömülmemiştir.
5. Ayrı bir yerel oyuncu FP görseli olarak bağla. Kamera-relative başlangıç transformu position/rotation `0`, scale `1`. Idle animasyonunu oynat; animasyon oynatılmazsa model dinlenme pozunda görünür. Omuz/kol ofsetleri klipte yer alır.
6. Sadece yerel oyuncu kamerası bu kolları göstermeli. Dünya modeli uzak oyuncular ve gölgeler için korunmalı; yerel görünümünde üst gövde/kolların iki kez çizilmesi engellenmeli. Bacaklar kamera pitch'ine parent edilmemeli. Bu runtime görünürlük/prefab bağlantısı henüz uygulanmadı.

## Kullanıcıya bırakılan kontroller

Unity import, FBX round-trip, materyal remap, animasyon döngüsü, FOV aralığı, clipping, multiplayer görünürlüğü ve Play Mode kabulü **çalıştırılmadı**. Blender'daki kadraj düzenlemesi oyun içi kabul testi değildir. Özellikle mevcut kamera near clip değeri ve değişken FOV ile el/bilek kadrajını kontrol et; kaynak kamera yalnızca önizleme için 0,01 m near clip kullanır.

FBX yalnızca seçili rig/mesh ve aktif animasyonla, leaf bones kapalı, NLA/all actions kapalı, animasyon sadeleştirme 0 olarak dışa aktarıldı. Dışa aktarma seçenekleri: [Blender FBX belgesi](https://docs.blender.org/manual/en/5.2/files/import_export/fbx_legacy.html).
