# PlayerExplorer — birleşik FPS gövdesi

8 Eylül 2026. Orijinal karakterden hazırlanmış, kolları omuzdan gövdeye bağlı kalan FPS model paketi. Eski `FirstPerson` klasöründeki ayrı kol denemesinden bağımsızdır.

## İçerik

- `PlayerExplorer_FPBody.blend`: dokuları bağlı çalışma dosyası. `Scene` orijinal karakter, `Explorer_FPBody` FPS çalışmasıdır. Kamera `FPBody_Eyes`, dış inceleme kamerası `FPBody_Review`.
- `Source_20260908.blend`: bu çalışmaya başlamadan alınan orijinal sahne kopyası.
- `SM_PlayerExplorer_FPBody_A.fbx`: baş ve boyun bağlantısı geometrisi çıkarılmış, gövde/kollar/bacaklar birlikte model. 3 skinned mesh, 108.698 üçgen, 101 kemik. Baş kemiği Humanoid uyumu için durur.
- `AN_FPBody_Relaxed.fbx`, `AN_FPBody_ToolReady.fbx`, `AN_FPBody_TwoHandReady.fbx`: rahat, tek el alet hazırlığı ve iki el tutuş hazırlığı. Her biri 30 fps, 1–61 kare, tam 2 saniyelik hafif nefes döngüsü. Eşya ile temas animasyonları değildir.
- `preview_tool.png`, `preview_twohand.png`, `preview_down.png`, `preview_body.png`: Blender kamera ve dış görünüş renderları; oyun ekran görüntüsü değildir.

Mevcut vertex konumları, UV'ler, parmak/twist kemikleri, skin ağırlıkları ve rest iskeleti korundu. Kopyada boş `V_None` shape key kaldırıldı. Yüzler açıkça üçgenlendi ve kaynaktaki 200 sıfır alanlı üçgen temizlendi. Omuz/kol uzunlukları değiştirilmedi. Orijinal FBX, prefablar ve gameplay kodu değiştirilmedi.

## Unity paketi

Hazır dosyalar `Assets/Project/Art/Models/Characters/PlayerExplorer/FPBody/` altındadır:

- `PF_PlayerExplorerFPBody_Visual.prefab`: Animator ve mevcut beyaz kostüm materyalleri bağlı, yalnızca görsel önizleme prefabı; fizik/input/network bileşeni içermez.
- `AC_PlayerExplorer_FPBodyPreview.controller`: üç pozun incelenmesi için controller; varsayılanı Relaxed. Gameplay locomotion controller'ı değildir.
- `AM_PlayerExplorer_FPArms.mask`: kol/parmak Humanoid kanalları ve clavicle altındaki twist transformları açık; root/gövde/baş/bacaklar kapalı. Tutuş pozlarını mevcut locomotion üzerine bir override layer ile uygulamak için hazırdır.

Model Humanoid olarak içe alındı. Klipler bu modelin Avatar'ını kullanır; `Preserve Hierarchy` açıktır. Root motion kapalıdır. Materyaller projedeki mevcut `MAT_PlayerExplorer_*_White` varlıklarına GUID ile bağlıdır; yeni dokular üretilmedi. Altı etkiye kadar kaynak skin ağırlığı import edilir; gerçek çizimde etkin etki sınırını projenin skin quality ayarı da belirler.

Bu prefab mevcut dünya karakterinin yerine otomatik geçirilmedi. Mevcut karakter üzerine ikinci görünür gövde olarak eklenmemeli. Yerel oyuncu görünürlüğü, mevcut Animator/FootIK, FishNet sahipliği ve tam gövdeli dünya gölgeleri entegrasyonda birlikte korunmalı. Model gölge çizimi bu nedenle kapalıdır. Klipleri mevcut karakter üzerinde kullanmak da mümkündür; hareketin sahibi `FirstPersonMotor` kalır.

## Kadraj

Önizleme 16:9, 60° dikey FOV, 0,05 m near clip kullanır. Blender'da ileri -Y, yukarı +Z. Göz, Head pivotundan `(0, -0.18, 0.065)` metredir. Unity karşılığı aday ofset `(0, 0.065, 0.18)`; mevcut oyunun `0.08` ileri ofseti değiştirilmedi. Aşağı bakış renderı 82° pitch kullanır. Bu aday kamera konumu koşu, iniş, eğimler ve duvar yakınında oyun içinde doğrulanmalıdır.

## Doğrulama ve sınır

`check_fp_body.py` Blender'da çalıştırıldı: vertex/UV/skin/rest iskeleti koruması, sıfır alanlı yüz bulunmaması, kol uzunlukları ve üç animasyonun döngü birleşimi kontrol edilir.

`FPBodyImportCheck.cs`, Unity 6000.4.5f1 ile ayrı geçici projede çalıştırılan import/prefab kontrolüdür. Gerçek projeye Editor script olarak kurulmaz. Kontrolün sonucu `Unity-validation.txt` dosyasındadır. Bu izole kontrol oyunun Play Mode kabulü değildir.

Oyun içi bağlantı henüz yapılmadı. Yürüme/koşma/yüzme ile katman geçişleri, pitch boyunca eşya takibi, gerçek aletin sapına göre parmak teması, duvar clipping'i, FOV tercihleri, cockpit geçişleri ve multiplayer görünürlüğü ayrıca uygulanıp doğrulanmalıdır. Hazırlanan pozlar ileri bakış için bir başlangıçtır; tamamlanmış ekipman animasyon sistemi olarak sunulmaz.

Tekrar üretim: kaynak sahnede `prepare_fp_body.py`, ardından `pose_fp_body.py`, `export_fp_body.py`, `check_fp_body.py`. Hazır çalışma dosyasında yalnızca gerekli adımı çalıştır; `prepare` mevcut sahneyi ezmez.

Dışa aktarım seçenekleri: [Blender FBX belgesi](https://docs.blender.org/manual/en/5.2/files/import_export/fbx_legacy.html). Eşya bazlı el hedefleri için projede kurulu Animation Rigging'in [Two Bone IK](https://docs.unity.cn/Packages/com.unity.animation.rigging%401.3/manual/constraints/TwoBoneIKConstraint.html) bileşeni kullanılabilir.
