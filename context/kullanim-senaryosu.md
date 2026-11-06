# Kullanım Senaryosu ve Model Standartları

Bu dosya, kullanıcının SketchUp'ı nasıl kullandığını ve Sketch'te birebir karşılanması gereken çalışma düzenini kaydeder. Kaynak: kullanıcının 2026-10-20 tarihli SketchUp 2026 ekran görüntüleri ve açıklaması.

## Kullanım amacı

Bina zarfı / termal eleman modellemesi: dış duvar, döşeme, çatı, iç duvar gibi elemanların yüzleri etiketlenir; alanlar Entity Info panelinden okunur. Model, kat bazında gruplara ayrılır (ör. 1F, 2F, 3F, NorthArrow).

## Kullanılan arayüz parçaları

- Sol ve üst araç çubukları (SketchUp'ın büyük araç seti ve standart araçlar).
- Üstte sahne sekmeleri: (Default), (All On), (All Off), (Thermal Elements), (Floor TE), (Wall TE), (Roof TE).
- Üst araç çubuğunda etkin etiket seçici (ör. "100 External Wall 1").
- Sağ panel (Tray): Entity Info (Tag seçimi, Area, Toggles), Materials, Outliner (grup ağacı, görünürlük gözleri).
- Model görünümü etiket rengine göre boyanır (Color by Tag).

## Malzemeler

Yalnızca iki malzeme kullanılır: **Glass** (pencereler) ve **Door** (kapılar; kullanıcının SketchUp dosyalarında ve DEAP eklentisinde "wood"). Kullanıcının isteğiyle (2026-10-21) "Glass Basic" ve "NonSlidingDoor" ayrı malzeme olarak kullanılmaz; SKP açılırken adında glass geçen malzemeler Glass'a, wood/door geçenler Door'a birleştirilir.

## Altlık resimler

Kullanıcı kat planlarını PNG olarak SketchUp'a alır ve üzerinden geçerek çizer (Block 4'te "1. Floor", "4. Floor", "5. Floor" gruplarında birer resim). Bu resimler SKP açılırken okunur.

## Etiket listesi (sırayla)

Untagged, 000 Dimension Check, 000 Import Plan, 000 Import Section, 001 Exposed Floor 1, 002 Exposed Floor 2, 003 Exposed Floor 3, 004 Exposed Floor 4, 005 Exposed Floor Living Area, 010 Semi-Exposed Floor 1, 011 Semi-Exposed Floor 2, 012 Semi-Exposed Floor 3, 013 Semi-Exposed Floor 4, 014 Semi-Exposed Floor Living Area, 020 Non-heat Loss Floor 1, 021 Non-heat Loss Floor 2, 022 Non-heat-loss Floor 3, 023 Non-heat Loss Floor 4, 024 Non-heat Loss Floor Living Area, 100 External Wall 1, 101 External Wall 2, 102 External Wall 3, 103 External Wall 4, 110 Semi-Exposed Wall 1, 111 Semi-Exposed Wall 2, 112 SEmi-Exposed Wall 3, 113 Semi-Exposed Wall 4, 120 Internal Wall 1, 121 Internal Wall 2, 122 Internal Wall 3, 123 Internal Wall 4, 200 Pitched Roof - flat ceiling 1, 201 Pitched Roof - flat ceiling 2, 202 Pitched Roof - flat ceiling 3, 203 Pitched Roof - flat ceiling 4, 210 Pitched Roof - sloped ceiling 1, 211 Pitched Roof - sloped ceiling 2, 212 Pitched Roof - sloped ceiling 3, 213 Pitched Roof - sloped ceiling 4, 220 Flat Roof Type 1, 221 Flat Roof Type 2, 222 Flat Roof Type 3, 223 Flat Roof Type 4, 230 Semi-Exposed Roof Type 1, 231 Semi-Exposed Roof Type 2, 232 Semi-Exposed Roof Type 3, 233 Semi-Exposed Roof Type 4, 240 Non-heat Loss Ceiling 1, 241 Non-heat Loss Ceiling 2, 242 Non-heat Loss Ceiling 3, 243 Non-heat Loss Ceiling 4, 998 Heated space, 998 Unheated space, 999 Air tightness envelope, 999 Null.

Not: Kullanıcının isteğiyle (2026-10-20) yinelenen etiketler ("025 Non-heat Loss Floor Living Area", "200 External Wall 1", "220 Internal Wall 1") şablondan çıkarıldı. "022 Non-heat-loss Floor 3" ve "112 SEmi-Exposed Wall 3" yazımları kullanıcının SketchUp dosyalarındaki adlarla aynı olduğu için korunur. Etiket renkleri ekran görüntüsünden okunamadığı için varsayılan renkler atanır; kullanıcı değiştirebilir.
