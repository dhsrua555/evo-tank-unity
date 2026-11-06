# 물기둥(깊이 10 m 호수·연안) 환경과 원생생물 형태 기능: 근거 수치

작성 2026-11-06. 조사는 WebSearch/WebFetch로 했고, 계산 검증은 python으로 직접 했다.

확인 등급:
- **[원문]** 논문 본문이나 표를 직접 읽음
- **[초록]** 초록을 직접 확인함(출판사, PMC, OpenAlex 초록 색인)
- **[2차]** 다른 논문, 코드, 검색 요약이 인용한 값. 원문 대조는 못 함
- **[계산]** 확인된 식으로 직접 계산함(코드로 재현)
- **[미확인]** 원문이나 초록을 확인하지 못함. 통념값이므로 쓸 때 주의

---

## 0. 핵심 요약 (게임 공식용)

| 변수 | 추천값 / 식 | 등급 |
|---|---|---|
| O2 포화 (담수, 1 atm) | 0 °C 14.62, 4 °C 13.11, 10 °C 11.29, 20 °C 9.09, 30 °C 7.56 mg/L | [계산] (Garcia & Gordon 1992 식, 검증값 재현) |
| 수면 기체 교환 k600 | 2.07 + 0.215·U10^1.7 cm/h. 바람이 없으면 약 0.5 m/d, 5 m/s에서 1.3 m/d | [원문] Cole & Caraco 1998 |
| k(O2) | k600·(Sc/600)^-0.5, Sc_O2 = 1568 − 86.04T + 2.142T² − 0.0216T³ (20 °C에서 531) | [2차] Raymond 2012 (LakeMetabolizer 코드) |
| 광합성 지수 PQ | NH4로 자랄 때 약 1.1, NO3로 자랄 때 약 1.4 (현장 측정 범위 0.1–4.2) | [2차] Laws 1991 / [초록] Trentman 2023 |
| 심수층 산소 소비 (부영양) | 면적당 0.47–1.31 g O2 m⁻² d⁻¹ (평균 0.90), 퇴적물 환원물질 몫 0.37 | [초록] Müller 2012 |
| PAR Kd | 맑은 호수 0.1, 탁한 호수 0.45–15 m⁻¹ | [원문]/[초록] |
| 엽록소 비감쇠 | 0.016–0.02 m² (mg Chl)⁻¹ | [초록]/[2차] |
| UV(320 nm) Kd | 맑은 호수 0.2–0.3, 탁한 호수 1.5–3.9 m⁻¹. UV-B 1% 깊이는 수 cm에서 10 m 이상까지 | [원문]/[초록] |
| 심수층 Kz (10 m 소호수) | 10⁻⁷–10⁻⁵ m² s⁻¹, Kz = a(N²)^b, b = −0.36 ~ −0.45 | [초록] Hondzo 1991 |
| 형태 저항 Φ | 편모충 0.48–0.77, 구형 1, Staurastrum 0.89–1.66, Pediastrum 약 1.9, Fragilaria 사슬 4.4–8.1 | [원문] Padisák 2003 |
| 세포 밀도 | 일반 1.04, 규조 최대 1.29 g cm⁻³, 규산 각 2.1–2.3, 가스 소포가 있는 남조 0.98 | [원문] (2차 인용 포함) |
| 가스 소포 붕괴압 | 0.1–1.2 MPa. 수심 10 m의 수압은 0.098 MPa에 불과함 | [초록]/[2차]/[계산] |
| 영양염 흡수 크기 지수 | Vmax ∝ V^0.67, Ks ∝ V^0.33 | [원문] Finkel 2010 |
| 길쭉함 효과 (같은 부피, 확산 한계) | 종횡비 4에서 +18%, 10에서 +54%, 20에서 +100% | [계산] |
| 유영 속도 | 편모충 중앙값 127 µm/s, 섬모충 784 µm/s | [원문] Lisicki 2019 |
| 편모 건설비 비율 | c_rel = 0.98·V^-0.66 (V는 µm³). 클라미도모나스는 1.4% | [원문] Schavemaker & Lynch 2022 |
| 유도 방어 비용 | Euplotes 성장률 −15% (r 0.60에서 0.51 d⁻¹로) | [2차] Kusch & Kuhlmann 1994 |
| 세포 탄소 | 원생생물 pgC = 0.216·V^0.939, 규조 pgC = 0.288·V^0.811 | [초록] Menden-Deuer & Lessard 2000 |
| 세균 / HNF / 섬모충 밀도 | 3×10⁵–1×10⁹ / 3×10²–4×10⁶ / 0.6–229 개 mL⁻¹ | [초록]/[2차] |
| 무산소 퇴적물 P 방출 | 빈영양 약 2.2, 부영양 약 14 mg P m⁻² d⁻¹ | [2차] Nürnberg 1986 |

---

## 1. 용존 산소

### 1.1 포화 농도 (온도·염분)

Garcia & Gordon (1992)의 "combined fit"(Benson & Krause 1984 자료)를 쓴다. 단위는 µmol/kg이며, 1 atm에서 수증기로 포화된 공기와 평형인 상태다.

```
Ts = ln((298.15 − t) / (273.15 + t))          # t: °C
ln C = A0 + A1·Ts + A2·Ts² + A3·Ts³ + A4·Ts⁴ + A5·Ts⁵
     + S·(B0 + B1·Ts + B2·Ts² + B3·Ts³) + C0·S²
A0=5.80871  A1=3.20291  A2=4.17887  A3=5.10006  A4=−9.86643e−2  A5=3.80369
B0=−7.01577e−3  B1=−7.70028e−3  B2=−1.13864e−2  B3=−9.51519e−3  C0=−2.75915e−7
mg/L = C[µmol/kg] × ρ[kg/L] × 0.0319988
```

검증: TEOS-10 `gsw_O2sol_SP_pt`의 검증값(SP 34.7118, pt 28.8099 °C에서 194.6825431692940 µmol/kg)을 t×1.00024(IPTS-68 환산)로 넣어 **소수점 이하 열두 자리까지 재현했다**. 따라서 계수가 맞다.

| t (°C) | 0 | 4 | 10 | 15 | 20 | 25 | 30 |
|---|---|---|---|---|---|---|---|
| S=0 (담수) mg/L | 14.62 | 13.11 | 11.29 | 10.08 | 9.09 | 8.26 | 7.56 |
| S=5 (기수) mg/L | 14.12 | 12.67 | 10.93 | 9.78 | 8.83 | 8.03 | 7.36 |
| S=35 (연안 해수) mg/L | 11.43 | 10.34 | 9.02 | 8.14 | 7.40 | 6.78 | 6.24 |

담수 밀도는 Kell(1975) 식으로 넣었다. 해수 밀도는 ρ + 0.00077·S로 근사했으며, 오차는 0.1% 수준이다. 담수 값은 USGS 표의 통념값(0 °C 14.6, 20 °C 9.09)과 일치한다. 대기압이 바뀌면 대략 P/1 atm에 비례한다(헨리 법칙. 수증기압 보정은 생략).

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 포화식 | Benson & Krause 자료를 고쳐 맞춘 식. 일반 용도로 권장됨 | 위 계수 | Garcia & Gordon 1992, L&O 37:1307, doi:10.4319/lo.1992.37.6.1307 (정오표 1993 L&O 38:656) | [계산] TEOS-10 검증값 재현 |
| 원자료 | 담수와 해수의 O2 평형 농도 | — | Benson & Krause 1984, L&O 29:620–632, doi:10.4319/lo.1984.29.3.0620 | [초록] 서지 확인 |

### 1.2 수면 기체 교환 (piston velocity)

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 저풍속 호수 k | Mirror Lake(15 ha)에 SF6를 넣어 측정. 여름 50일 동안 바람과 무관 | k600 = 2.65 ± 0.12 cm/h (0.64 m/d). 바람보다 강수와 상관이 큼 | Cole & Caraco 1998, L&O 43:647–656 | [원문] |
| 바람 관계 | 10개 호수 192점에 맞춤. 바람이 0이어도 약 2 cm/h | k600 = 2.07 + 0.215·U10^1.7 (cm/h), r² = 0.61 | Cole & Caraco 1998 식 5 | [원문] |
| 비교식 | Wanninkhof 1992, MacIntyre 1995 | k600 = 0.45·U10^1.64. 3–5 m/s에서는 식 5와 같고, 저풍속에서 갈라짐 | Cole & Caraco 1998 식 6 | [원문] |
| Sc 환산 | k_gas = k600·(Sc/600)^-n, n = 0.5 | — | LakeMetabolizer `k600.2.kGAS` 코드 | [2차] |
| O2 슈미트 수 | 담수 O2 | Sc = 1568 − 86.04T + 2.142T² − 0.0216T³ (20 °C에서 531, 17.5 °C에서 약 600) | Raymond et al. 2012, L&O:F&E 2:41–53, doi:10.1215/21573689-1597669 (LakeMetabolizer `getSchmidt`) | [2차] 코드에서 확인 |

계산된 k(O2) [계산]:

| U10 (m/s) | k600 (m/d) | kO2 5 °C (m/d) | kO2 20 °C (m/d) |
|---|---|---|---|
| 0 | 0.50 | 0.35 | 0.53 |
| 2 | 0.66 | 0.47 | 0.71 |
| 5 | 1.29 | 0.92 | 1.37 |
| 8 | 2.27 | 1.61 | 2.41 |

게임 식: 맨 위 칸의 두께를 Δz라 하면 `dC/dt = kO2·(Csat − C)/Δz`. 예를 들어 Δz = 0.5 m, kO2 = 1 m/d이면 맨 위 칸이 포화 쪽으로 다가가는 시간상수는 약 0.5일이다.

### 1.3 광합성 산소 생산과 호흡 소비

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| PQ (질소원 의존) | NO3로 자랄 때는 질산 환원에 O2가 더 나온다 | NO3 약 1.4, NH4 약 1.1 (mol O2 / mol C) | Laws 1991, Deep-Sea Res A 38:143–167, doi:10.1016/0198-0149(91)90059-O | [2차] Iriarte 1999 등이 인용. 원문 미열람. 다른 인용에는 NO3 1.3, NH4 1.0도 있음 |
| PQ 현장 범위 | 생태학자들은 보통 1–1.4로 고정해 쓰지만, 문헌 값은 훨씬 넓다 | 고정값 1–1.4, 문헌 범위 0.1–4.2 (담수 측정 0.13–2.0) | Trentman, Hall & Valett 2023, L&O Letters 8:565–579, doi:10.1002/lol2.10326 | [초록] (담수 범위는 preprint에서 [2차]) |
| 호흡 크기 지수 | 자라는 원생동물의 대사율은 무게^0.75에 비례한다. 굶기 시작하면 빠르게 떨어지고, 성장 중에는 성장률에 거의 선형 비례 | R ∝ W^0.75 (계수 미확인) | Fenchel & Finlay 1983, Microb Ecol 9:99–122, doi:10.1007/BF02015125 | [초록] (계수는 [미확인]) |
| RQ | 탄수화물 호흡은 O2 1 mol당 CO2 1 mol | RQ ≈ 1 | 화학양론 | [계산] |

게임 식 [계산]: 고정한 C 1 g당 생산되는 O2는 PQ × 32/12 g이므로 NO3 성장이면 3.73 g O2, NH4 성장이면 2.93 g O2. 호흡한 C 1 g당 소비되는 O2는 (32/12)/RQ = 2.67 g.

### 1.4 성층 호수 심수층 산소 고갈

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 부영양 호수 11곳 | 심수층 무기화는 가라앉은 유기물 분해와 퇴적물에서 올라오는 환원물질 산화의 합 | AHM 0.47–1.31 (평균 0.90 ± 0.30) g O2 m⁻² d⁻¹. 환원물질 플럭스 0.37 ± 0.12. 심수층 두께 약 25 m까지는 두께에 선형 비례 | Müller, Bryant, Matzinger & Wüest 2012, Environ Sci Technol 46:9964, doi:10.1021/es301422r | [초록] |
| P 문턱 | 21개 호수. 면적당 P 공급이 문턱을 넘으면 소비율이 일정함 | APS > 0.54 ± 0.06 g P m⁻²이면 AHM = 1.06 ± 0.08 g O2 m⁻² d⁻¹. 문턱 아래에서만 APS에 비례해 감소 | Müller et al. 2019, Sci Rep 9:18054, doi:10.1038/s41598-019-54486-3 | [초록] |
| 부피당 소비 | 32개 호수. 부피당 소비율은 영양 상태와 관련 있고, 면적당은 관련 없음 | JV 최대 약 0.23–0.24 g O2 m⁻³ d⁻¹ (부영양). 퇴적물 전체 소비율 R은 7–460 g O2 m⁻³ d⁻¹ (10개 호수) | Rippey & McSorley 2009, L&O 54:905–916, doi:10.4319/lo.2009.54.3.0905 | [초록] (JA 숫자는 색인이 깨져 미확인) |
| 얼음 밑 겨울 | 고갈 속도는 평균 수심에 반비례 | 퇴적물 소비 부영양 0.23, 빈영양 0.08 g m⁻² d⁻¹. 수층 호흡은 둘 다 약 0.01 g m⁻³ d⁻¹. 3.8 mg/L 아래에서는 공급 제한 | Mathias & Barica 1980, CJFAS 37:185–194, doi:10.1139/f80-024 | [초록] |
| 깊이 의존 | 깊이 100 m 미만 호수에서는 퇴적물 표면 과정이 산소 소비를 주도한다 | — | Steinsberger et al. 2020, L&O, doi:10.1002/lno.11578 | [초록] (일부) |

10 m 호수 점검 [계산]: 심수층이 5–10 m(두께 5 m)이고 봄 전도 직후 4 °C에서 약 12 mg/L로 시작하면, 저장량은 약 60 g O2 m⁻²다. AHM이 1 g m⁻² d⁻¹이면 **약 60일 만에 바닥이 무산소가 된다**. 빈영양(0.08–0.3)이면 200일 이상 버틴다. 따라서 영양염 증가 → 생산 증가 → 침강 유기물 증가 → 바닥 무산소가 수치상 저절로 나온다.

---

## 2. 빛과 자외선

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| PAR Kd 범위 (탁한 호수) | 26개 호수·저수지, 206지점. 총부유물질이 지배함 (R² = 0.906) | Kd(PAR) 0.45–15.04 m⁻¹ | Ma et al. 2016, Remote Sens 8:964, doi:10.3390/rs8110964 | [초록] |
| PAR, UV Kd (맑은 고산호와 탁한 고산호) | 두 고산호 비교 | 맑은 호수: Kd PAR 0.1, Kd320 0.2–0.3 m⁻¹. 탁한 호수: Kd PAR 0.5–1.3, Kd320 1.5–3.9 m⁻¹ | Kammerlander et al. 2016, Freshw Biol 61:1950–1965, doi:10.1111/fwb.12828 (표 1) | [원문] |
| UV와 DOC | 호수 65곳에서 305, 320, 340, 380 nm와 PAR 측정. DOC가 호수 간 Kd 변이의 87–96%를 설명. UV 전 범위에서 DOC 단일 거듭제곱 모델이 가장 잘 맞음 | UV-B 1% 깊이는 수 cm에서 10 m 이상까지. 용존 흡광이 Kd에서 차지하는 몫은 PAR 33%, 305 nm 68% | Morris et al. 1995, L&O 40:1381–1391, doi:10.4319/lo.1995.40.8.1381 | [초록] |
| Kd(305) 식 | DOC의 거듭제곱 | Kd(305) ≈ 2.76·DOC^1.23 (DOC mg/L) | Morris 1995 (비와호 논문이 재인용) | [2차] |
| Kd(PAR) 선형식 | DOC와 Chl의 선형 모델 | Kd = 0.22·DOC + 0.07·Chl − 0.05 (DOC mg/L. Chl 단위는 인용문에 "g m⁻³"로 적혀 있으나 µg/L일 가능성이 큼) | Morris 1995 (Zwart et al. 2015 Ecology 부록 A 인용) | [2차] |
| 엽록소 비감쇠 | 식물플랑크톤 바이오매스의 비감쇠계수 | 0.019 m² (mg Chl a)⁻¹ | Loiselle et al. 2008, Freshw Biol (빅토리아호) | [초록] (검색 요약) |
| 엽록소 비감쇠 | PAR | 0.02 m² mg⁻¹ | Sakamoto & Hogetsu 1969 (Nõges 2000 Geophysica 인용) | [2차] |
| Secchi 환산 | 1% 광 깊이를 Secchi 깊이 × 2로 둠 | Kd ≈ 2.3 / SD | Zwart et al. 2015 Ecology 부록 A | [원문] |
| Secchi 환산 (고전) | — | Kd ≈ 1.7 / SD | Poole & Atkins 1929 | [미확인] |
| Ik | 고위도 북극 호수 7곳 | Ek 50–196 µmol m⁻² s⁻¹. 수층 평균 광량과 양의 상관 | Markager, Vincent & Tang 1999, L&O 44:597, doi:10.4319/lo.1999.44.3.0597 | [초록] |
| Ik | 멕시코 도시 호수 2곳의 나노플랑크톤 | Ik 18–295 µE m⁻² s⁻¹. 최대값은 Microcystis, Oscillatoria가 우점할 때 | Rev Biol Trop (revistas.ucr.ac.cr/index.php/rbt/article/view/26137) | [초록] (저자 미확인) |
| 분류군 차이 | 와편모조와 남조는 약광 최적이고 낮은 광도에서 광저해됨. 규조는 강광까지 견디고 1 µE m⁻² s⁻¹ 미만에서도 자라는 종이 있음 | — | Richardson, Beardall & Raven 1983, New Phytol 93:157–191, doi:10.1111/j.1469-8137.1983.tb03422.x | [초록] |
| C:Chl (θ⁻¹) | C:Chl은 광도에 선형 증가하고, 온도가 오르면 감소 | 50 µmol m⁻² s⁻¹에서 C:Chl 10 (30 °C)에서 130 (0 °C)까지. 절편 6–40 g C (g Chl)⁻¹ | Geider 1987, New Phytol 106:1–34, doi:10.1111/j.1469-8137.1987.tb04788.x | [초록] (색인 일부 깨짐) |
| Chl 정규화 P-I | 미세조류의 Chl 비 광제한·광포화 속도는 조건에 따라 20% 미만으로 변함 | <20% | MacIntyre et al. 2002, J Phycol 38:17–38, doi:10.1046/j.1529-8817.2002.00094.x | [초록] |
| 세포당 Chl 크기 지수 | — | 광제한에서 Chl/cell ∝ V^(2/3), 광포화에서 V^(3/4) | Finkel et al. 2010 (5절) | [원문] |

세포당 엽록소 예시 [계산] (Menden-Deuer 원생생물 탄소식에 C:Chl = 50을 가정):

| V (µm³) | 지름(구) | pgC/cell | pg Chl/cell |
|---|---|---|---|
| 10 | 2.7 µm | 1.9 | 0.04 |
| 100 | 5.8 µm | 16 | 0.33 |
| 1,000 | 12.4 µm | 142 | 2.8 |
| 10,000 | 26.7 µm | 1,232 | 25 |

C:Chl은 약광과 따뜻한 물에서 약 20, 강광과 찬 물에서 약 100 이상이다. 게임에서는 θ를 광 적응 변수로 두면 된다.

게임 식 제안:
- `I(z) = I0·exp(−Σ Kd·Δz)`, `Kd = Kw + 0.02·Chl[mg m⁻³] + kDOC·DOC + kTSS·TSS`. 순수 물 Kw는 약 0.03–0.05 m⁻¹ [미확인].
- P-I 곡선은 `P = Pmax·tanh(I/Ik)`로, Ik는 50–300 µmol m⁻² s⁻¹. 강광 광저해 항은 선택. 와편모조와 남조는 Ik를 낮게, 규조는 넓게 둔다.
- UV-B 피해는 `Kd_UV = 2.76·DOC^1.23`로 깊이에 따라 줄인다. DOC가 낮은 맑은 호수에서는 10 m 전체가 UV에 노출된다.

---

## 3. 섞임 (와확산, 혼합층, 전도)

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 성층 호수 Kz 범위 | 문헌의 일반 범위 | 10⁻⁹–10⁻² m² s⁻¹ (Alchichica호는 10⁻¹⁰–10⁻⁶) | Salas de León et al. 2016, J Limnol 75(s1):161–168, doi:10.4081/jlimnol.2016.1431 | [원문] |
| 10 m 소호수의 심수층 Kz | 면적 0.06 km², 최대 수심 10 m, 1989년 5–8월 성층 | Kz 10⁻³–10⁻¹ cm² s⁻¹ = **10⁻⁷–10⁻⁵ m² s⁻¹**. Kz = a(N²)^b, a는 약 10⁻⁴, b = −0.36 ~ −0.45 (Kz cm²/s, N s⁻¹) | Hondzo, Ellis & Stefan 1991, J Hydraul Eng 117(10):1352, doi:10.1061/(ASCE)0733-9429(1991)117:10(1352) | [초록] |
| 일반식 | Kz = 8.17×10⁻⁴·As^0.56·(N²)^−0.43 (cm²/s, As km²) | 0.06 km²에서 a ≈ 1.7×10⁻⁴로 위 초록과 맞음 | Hondzo & Stefan 1993, J Hydraul Eng 119(11):1251–1273 | [미확인] 계수 (형태와 지수만 1991 초록과 일치) |
| 고전 값 | — | Linsley Pond 3.3×10⁻⁷, Sodom 7×10⁻⁷, Mendota 2.5×10⁻⁶ m² s⁻¹. 휴런호·이리호 수온약층(염료 실험) 7×10⁻⁴ | Hutchinson 1957, Csanady 1964 (Salas de León 2016 인용) | [2차] |
| 퇴적물 근처 (얼음 밑) | — | 3.72 ± 1.41 ×10⁻³ cm² s⁻¹ (3.7×10⁻⁷ m² s⁻¹) | Mathias & Barica 1980 | [초록] |
| 내부 난류 | 내부 성층 난류는 극히 약하다. 경계(바닥)에 닿으면 분지 규모 혼합이 10배 이상 커진다 | — | Wüest & Lorke 2003, Annu Rev Fluid Mech 35:373–412, doi:10.1146/annurev.fluid.35.101101.161220 | [초록] (일부) |
| 분자 열확산 | — | 약 1.4×10⁻⁷ m² s⁻¹ | 물성 | [미확인] (통념값) |
| 수온약층 깊이와 취송거리 | 수온약층 깊이는 취송거리(fetch)와 함께 깊어진다 | Zt ≈ 5.8·F^0.37 (m, F는 km). 예: F = 0.3 km이면 약 3.7 m | Gorham & Boyce 1989, J Great Lakes Res 15:233–245 (Cruikshank 1984 OCR 인용) | [2차] (지수 미확인) |
| 혼합 분류 | 유형 경계는 위도·고도·수심에 따름. 얼음과 혼합 빈도로 8유형(다회 혼합 4유형) | — | Lewis 1983, CJFAS 40:1779–1787, doi:10.1139/f83-207 | [초록] |

물 밀도 (Kell 1975, kg m⁻³) [계산]: 0 °C 999.840, **4 °C 999.972 (최대, 3.98 °C)**, 10 °C 999.700, 15 °C 999.100, 20 °C 998.204, 25 °C 997.045, 30 °C 995.647.
식: `ρ = (999.83952 + 16.945176t − 7.9870401e−3t² − 46.170461e−6t³ + 105.56302e−9t⁴ − 280.54253e−12t⁵) / (1 + 16.879850e−3t)`.

게임 식 제안 [계산]:
- `N² = −(g/ρ)·dρ/dz`, 심수층·수온약층은 `Kz[m²/s] = 1e−8·(N²)^−0.4`. 이는 a = 10⁻⁴ cm²/s를 m²/s로 바꾼 값이다. 검산: N² = 10⁻³ s⁻²(강한 수온약층)이면 1.6×10⁻⁷, N² = 10⁻⁵이면 1.0×10⁻⁶ m²/s로 Hondzo 범위 안에 든다.
- 혼합층(표층)은 바람에 따라 Kz를 10⁻⁴–10⁻² m²/s로 두거나, 혼합층 안을 한 번에 균일화한다.
- **전도**: 가을에 표층이 식어 표층 밀도가 아래층 이상이 되면 아래로 균일화한다(대류 조정). 이때 심수층의 무산소 물과 축적된 P가 표층으로 올라와 가을 대증식이 생길 수 있다. 겨울 얼음 밑에서는 0 °C 표층 위, 4 °C 바닥 아래인 역성층이 된다. 봄에 다시 전도한다(이회 혼합). 10 m, 0.06 km² 호수도 여름 내내 성층했다(Hondzo 1991).
- 혼합층이 깊을수록 표층 수온은 낮다. 같은 열 입력을 더 두꺼운 층에 나누기 때문이다(열수지 계산).

---

## 4. 침강과 형태

### 4.1 Stokes 식과 형태 저항

`v = 2·g·r²·(ρ' − ρ) / (9·η·Φ)`. 여기서 r은 같은 부피 구의 반지름, Φ는 형태 저항이다. Φ > 1이면 같은 부피 구보다 느리게 가라앉는다 (Padisák et al. 2003 식) [원문].

Padisák, Soróczki-Pintér & Rezner 2003, *Hydrobiologia* 500:243–257, doi:10.1023/A:1024613001147 (PVC 모형을 글리세린에 떨어뜨림) [원문]:

| 형태 | Φ | 비고 |
|---|---|---|
| Gymnodinium helveticum | 0.476 | 편모충은 구보다 빠르게 가라앉음 |
| Rhodomonas lens / lacustris | 0.676 / 0.773 | |
| Cosmarium (매끈함 / 돌기) | 0.810–0.856 | |
| Staurastrum (팔 길이에 따라) | 0.886–1.661 | 팔 길이 / 세포 폭과 Φ가 양의 상관 |
| Ceratium hirundinella | 1.609 | 편모충 중 예외 |
| Pediastrum (원판) | 1.785–2.008 | 가라앉는 자세에 따라 달라짐. 대칭 여부와는 무관 |
| Asterionella 군체 | 1세포에서 6세포까지 급증한 뒤 평탄 | 대칭 군체가 비대칭보다 높음 |
| Fragilaria crotonensis 사슬 | ≤8세포 4.4–5.2, 20세포 **8.1 (최대)** | 8세포를 넘으면 급증 |
| 원기둥 | L/D와 양의 상관 (r = 0.79). L/D < 2에서는 무관 | 가로로 놓이면 더 느림 |
| 감긴 사상체 | 많이 감을수록 Φ 감소 | 곧은 사상체가 최대 |
| Tetrastrum 가시 | 가시의 수와 길이가 늘면 Φ 크게 증가. 비대칭 배열이면 가시 없는 것보다도 낮음 | |
| 식물플랑크톤 전반 | 0.94–5.49 | Reynolds 1984 (Padisák이 인용) [2차] |

### 4.2 밀도, 점성, 침강 속도

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 세포 밀도 | 무거운 규조와 기타 분류군 | 규조 최대 1.29 g cm⁻³, 기타 분류군 1.04 g cm⁻³ | Reynolds 1984 (Padisák 2003 인용) | [원문]의 인용 |
| 성분 밀도 | — | 규산 각 2.1–2.3, 단백질 1.3–1.4, 지질 0.90–0.93, TEP 0.70–0.84 g cm⁻³. 남조는 가스 소포로 1.04에서 0.98로 | Zhu et al. 2026, Biology 15:130, doi:10.3390/biology15020130 (Walsby 인용 포함) | [원문] 리뷰 |
| 석회판 | 방해석 밀도 | 2.71 g cm⁻³ | 광물 물성 | [미확인] (통념값) |
| 침강 속도 예 | — | Coscinodiscus 0.86 ~ >12 m/d. Skeletonema 0.33–0.57. E. huxleyi 0.12–0.55 (탄수화물이 쌓이면 2–4). 와편모조 보통 <1 (음수도 있음). Chaetoceros 성장 중 <1, 세포자살 때 최대 50. 응집체 >200 m/d | Zhu et al. 2026 | [원문] 리뷰 |
| 영양 제한 | — | Si 제한이면 침강 1.5–4배, N·P 제한이면 20–60% 감소 | Zhu et al. 2026 (ref 16 인용) | [원문] 리뷰 |
| 온도 | — | 9 °C 오르면 평균 침강 약 +40% | Zhu et al. 2026 (ref 31 인용) | [원문] 리뷰 |
| 고전 범위 | 0–30 m/d 수준 | — | Smayda 1970, Oceanogr Mar Biol Annu Rev 8:353–414 | [미확인] 수치 |
| 물 점성 | IAPWS 2008 | 10 °C 1.3059, 20 °C 1.0016, 30 °C 0.7972 mPa·s. 0 °C 1.792 | Anton Paar wiki (IAPWS 2008). 0 °C는 chem-casts | [2차] |
| 점성 근사식 | Vogel 식 | μ = 0.02939·exp(507.88/(T − 149.3)) mPa·s (T는 K). 10–30 °C에서 0.3% 이내, 0 °C에서 1.775 (−1%) | 위 표와 비교 | [계산] |

Stokes 예시 (20 °C, Φ = 1) [계산]:

| 지름 | Δρ (kg m⁻³) | v (m/d) |
|---|---|---|
| 2 µm | 40 | 0.008 |
| 10 µm | 50 | 0.24 |
| 20 µm | 100 | 1.9 |
| 50 µm (규조) | 250 | 29 |
| 10 µm | −50 (가스 소포·지질) | −0.24 (떠오름) |

0 °C의 점성은 30 °C의 2.2배이므로, 겨울에는 같은 세포가 약 2.2배 느리게 가라앉는다.

### 4.3 가스 소포 (부력)

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 구조 | 속이 빈 단백질 구조. 물은 막고 기체는 통과시킴. 임계압에서 납작하게 붕괴하면 부력을 잃음. 좁은 소포일수록 강함 | 폭 45 ~ >200 nm | Walsby 1994, Microbiol Rev 58:94–144, doi:10.1128/mr.58.1.94-144.1994 (PMC372955) | [초록] |
| 조절 | 팽압으로 붕괴시킬 수 없을 만큼 강한 소포를 가진 종은 광합성 탄수화물을 쌓고 쓰는 것으로 부력을 조절 | — | Walsby 1994 | [초록] |
| 붕괴압 | Planktothrix rubescens (취리히호 81주) | 0.86–1.17 MPa (쌍봉 분포) | Bright & Walsby 1999, Microbiology 145:2769, doi:10.1099/00221287-145-10-2769 | [초록] (검색 요약) |
| 붕괴압 | 발트해 남조 | Aphanizomenon 0.91 MPa, Nodularia 0.83 MPa(깊은 분지) / 0.34 MPa(연안). 팽압 0.08–0.18 MPa. 소포가 절반까지 붕괴해도 뜸 | Walsby, Hayes & Boje 1995, Eur J Phycol 30:87–94, doi:10.1080/09670269500650851 | [초록] |
| 붕괴압 | Anabaena (GvpC를 제거하면 약해짐) | 0.63 MPa에서 0.20 MPa로 | Brookes, Ganf & Burch 1994, Mar Freshw Res 45:863 (초록 요약) | [2차] |
| 붕괴압 | Halobacterium (가장 약함) | 약 0.1 MPa | Lakshmanan et al. 2017 (bioRxiv 2021 인용) | [2차] |
| 팽압 붕괴 | 강광 16시간에 팽압이 0.24 MPa 올라 0.54 MPa가 되고 소포 절반 이상이 붕괴 | — | Kinsman, Ibelings & Walsby 1991, J Gen Microbiol | [2차] |
| 군체 밀도 | 소포가 온전할 때와 붕괴한 뒤 | 975 kg m⁻³ → 1022 kg m⁻³ | 출처 미상 (검색 요약) | [미확인] |
| 소포 자체 밀도 | — | 약 120 kg m⁻³ | — | [미확인] |
| 수압 | 10 m 깊이 | 0.098 MPa | ρgh | [계산] |

게임 함의 [계산]: 깊이 10 m에서 수압만으로는 남조 소포(0.3–1.2 MPa)가 붕괴하지 않는다. 붕괴 조건은 `팽압 + 수압 ≥ Pcrit`이다. 강광 → 탄수화물·팽압 증가 → 일부 붕괴 → 가라앉음, 약광 → 탄수화물을 호흡으로 소모 → 다시 떠오름. 이 과정이 하루 주기 부침을 만든다. 소포 폭(강도)과 소포당 부력 사이에 트레이드오프가 있다(넓을수록 싸지만 약함).

---

## 5. 표면적 대 부피와 영양염 흡수, package effect

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 확산 한계 흡수 | 구로의 확산 플럭스는 4πDrC∞로 r에 비례하고, 수요는 r³에 비례하므로 부피당 공급은 r⁻² | — | Chisholm 1992, 『Primary Productivity and Biogeochemical Cycles in the Sea』(Falkowski & Woodhead 편, Plenum) pp. 213–237, 책 doi:10.1007/978-1-4899-0762-2 | 식은 [계산] (기본 물리). 장 본문은 [미확인] |
| 흐름 효과 | 흐름 영역마다 해가 다르다. 크기와 유속이 클수록 물질전달 증가. 고유속에서는 형태가 중요 | Sh = 1 + 0.29·Pe^½ (Pe < 0.01), 1.014 + 0.51·Pe^½ (0.01 < Pe < 100), 0.55·Pe^⅓ (Pe > 100) | Karp-Boss, Boss & Jumars 1996, Oceanogr Mar Biol Annu Rev 34:71–107 (Ploug et al. 1999 인용식) | [초록] 정성, [2차] 식 |
| 형태와 사슬 | 길쭉한 단세포는 같은 부피 구보다 확산 공급이 많다. 사슬은 단세포보다 적지만 난류 이득은 상대적으로 크다. 세포 사이에 틈이 있는 사슬만 단세포를 넘을 수 있다 | — | Pahlow, Riebesell & Wolf-Gladrow 1997, L&O 42:1660–1672, doi:10.4319/lo.1997.42.8.1660 | [초록] |
| 크기 지수 | — | Vmax ∝ V^0.67, Ks ∝ V^0.33. 세포 C·N·P ∝ V^0.7–0.9. 질량 비 대사 b = −0.25. 성장 log μ = −0.06·log V + 0.1 (r² 0.15). 침강 ∝ V^(2/3) | Finkel et al. 2010, J Plankton Res 32:119–137, doi:10.1093/plankt/fbp098 | [원문] |
| package effect | 세포 내 색소 농도가 높을수록 package effect가 크다. 반지름을 줄이면 상쇄됨. 효과는 약 10% 수준 | 광제한에서 a*(Chl 비흡수) ∝ V^−0.08 | Finkel et al. 2010 (Finkel 2001, Finkel 2004 인용) | [원문] |
| package effect 기전 | 규조 8종. 성장과 호흡의 크기 지수는 약 −1/4. 탄소 비 광합성이 벗어나는 원인이 package effect | — | Finkel 2001, L&O 46:86–94, doi:10.4319/lo.2001.46.1.0086 | [초록] |

길쭉함과 원판 효과 (같은 부피, 순수 확산, 회전타원체 정전용량) [계산]:

| 종횡비 | 장구형(바늘) 플럭스 / 구 | 표면적 / 구 | 편구형(원판) 플럭스 / 구 |
|---|---|---|---|
| 2 | 1.04 | 1.08 | 1.04 |
| 4 | 1.18 | 1.28 | 1.17 |
| 10 | 1.54 | 1.70 | 1.46 |
| 20 | 2.00 | 2.13 | — |

식: 장구형 a > b = c이고 a·b² = r³일 때 C' = √(a² − b²) / arccosh(a/b). 플럭스 = 4πD·C'·C∞. 표면적이 늘어난 만큼 플럭스가 다 늘지는 않는다(표면적당 플럭스는 감소).

Péclet 예시 [계산] (영양염 D ≈ 1×10⁻⁹ m²/s로 가정 [미확인]): 10 µm 세포가 1 m/d로 움직이면 Pe = 0.058, Sh = 1.14 (+14%). 50 µm 세포가 10 m/d로 움직이면 Pe = 2.9, Sh = 1.9 (+90%). **움직임이 흡수에 주는 이득은 큰 세포에서만 의미가 있다.**

게임 식 제안: `uptake = min(Vmax·S/(Ks + S), 4πD·C'·Sh·S)`, `Vmax ∝ V^0.67`, `Ks ∝ V^0.33`.
트레이드오프: 작으면 흡수는 유리하지만 포식에 취약하다(포식자 입 크기). 길쭉하면 흡수와 Φ가 함께 늘어 침강이 느려지지만, 표면적당 건설비가 든다.

---

## 6. 방어 형태와 유도 방어

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 리뷰 (저널 확인) | 방어는 생리·형태·행동 형질로 다양하다. 많은 방어의 비용이 정량화되지 않았다. 어떤 방어는 비용이 없고, 어떤 비용은 자연 조건(침강 손실)이나 자원 부족 조건에서만 드러난다 | 초록에 수치 없음 | Pančić & Kiørboe 2018, **Biological Reviews** 93(2):1269–1303, doi:10.1111/brv.12395 | [초록] |
| Euplotes 유도 방어 | 포식자가 내는 물질(형태 형성 인자)에 반응해 수 시간 안에 세포 분열 없이 형태가 바뀌어 삼키기 어려워진다. 인자 농도가 유지되면 분열 후에도 지속 | — | Kuhlmann & Heckmann 1985, **Science** 227:1347–1349, doi:10.1126/science.227.4692.1347 | [초록] |
| Euplotes 방어 비용 | Stenostomum이 유도한 날개형 | 개체군 성장률 r 0.60 → 0.51 d⁻¹ (**−15%**). 세대 시간 40 ± 1 → 47 ± 2 h. 날개형의 섭식률은 오히려 약간 높아 먹이 제한이 비용 원인은 아님 | Kusch & Kuhlmann 1994, Arch Hydrobiol 130:257–267, doi:10.1127/archiv-hydrobiol/130/1994/257 | [2차] (NIES 초록 데이터베이스) |
| Euplotes 방어 형태 | Lembadion이 있으면 세포 길이와 폭이 증가 (측면 날개) | 수치 미확인 | 검색 요약 | [미확인] 크기 % |
| 포식자 반격 | Lembadion은 먹이에 맞춰 몸 크기와 입 크기를 키운다(유도 공격). 큰 형은 작은 먹이에서 부피당 섭식률과 최대 성장률이 낮다 | — | Kopp & Tollrian 2003, Ecology 84:641–651 | [원문] 초록 |
| Scenedesmus 군체 | 초식자(Daphnia)의 화학 신호로 군체를 만든다. 탄소·광 제한인 자연 조건에서 효과적 | — | Lürling 2003, Ann Limnol 39:85–101, doi:10.1051/limn/2003014 | [초록] |
| 군체 비용 | 성장률과 PSII 효율은 차이가 없고 군체가 더 빨리 가라앉는다. 비용은 주로 유광층에서 침강 손실 | 수치는 초록에 없음 | Lürling & Van Donk 2000, Oikos 88:111–118, doi:10.1034/j.1600-0706.2000.880113.x | [초록] |
| 규산 각 비용 | 규산 구조는 같은 부피의 유기 벽보다 에너지가 덜 든다 | 다당류 대비 약 1/10, 리그닌 대비 약 1/20 | Raven 1983, Biol Rev 58:179 (Hendry et al. 2018 Front Mar Sci 5:22, doi:10.3389/fmars.2018.00022 인용) | [2차] |
| 각 강도 | 규조 각은 밀도당 강도가 알려진 생물 재료 중 최고. 초식자 신호에 규산화가 늘어남 | — | Hamm et al. 2003, Nature 421:841, doi:10.1038/nature01416, Pondaven et al. 2007 (Hendry 2018 인용) | [2차] |
| 가시 | 가시의 수와 길이가 늘면 Φ 증가 (부유에 유리, 방어와 시너지) | 4.1절 | Padisák 2003 | [원문] |

게임 수치 제안:
- 유도 방어 발현 시 성장률 −10 ~ −20% (Euplotes −15% 근거). 발현 지연은 수 시간.
- 군체: 성장 비용은 0, 대신 r_eq 증가로 침강 속도가 크게 오른다. 예: 4세포 군체는 r_eq가 4^(1/3) = 1.59배이므로 침강이 2.5배 (Φ 보정 전).
- 규산 각: 밀도 페널티(침강 증가) 대신 건설비는 유기 벽의 1/10, 방어 효과는 큼.

---

## 7. 섬모 대 편모

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 유영 속도 분포 | 편모충과 섬모충 모두 로그정규 분포 | 중앙값: 편모충 127 µm/s (σ 0.978), 섬모충 784 µm/s (σ 0.936). 편모충 9.7 µm/s (Trypanosoma)에서 1500 µm/s (Peridinium)까지. 섬모충 50 µm/s (Opalina)에서 7350 µm/s (Mesodinium, L = 38 µm)까지 | Lisicki, Rodrigues, Goldstein & Lauga 2019, eLife 8:e44907, doi:10.7554/eLife.44907 | [원문] |
| 크기 범위 | — | 편모충 L 2–397.5 µm (평균 31). 섬모충 L 25–1045 µm (평균 132) | Lisicki 2019 부록 | [원문] |
| 체장/초 | — | 편모충 약 4, 섬모충 약 6 (중앙 속도 / 평균 길이) | — | [계산] |
| 편모 비용 (클라미도모나스) | V = 122 µm³, 11 µm 편모 2개, 분열 9.15 h | 편모 1개 건설 3.08×10¹⁰ ATP, 운영 9.7×10⁵ ATP/s. 세포 총비용의 건설 1.4%, 운영 1.2% | Schavemaker & Lynch 2022, eLife 11:e77266, doi:10.7554/eLife.77266 | [원문] |
| 크기 스케일링 | 진핵 편모충 | 상대 건설비 c_rel = 0.98·V^−0.66. 절대 건설비 c_abs = 2.6×10¹⁰·V^0.31 ATP. 섬모충·파라바살리드 c_abs = 2.8×10⁹·V^0.86 | Schavemaker & Lynch 2022 | [원문] |
| 범위 | — | 진핵생물 상대 건설비 0.037–44% (중앙값 3.0%). 세균 0.43–96% (중앙값 3.7%) | Schavemaker & Lynch 2022 | [원문] |
| 결론 | 진핵 편모는 전형적인 원핵생물보다 큰 세포에서만 감당할 수 있다 | — | Schavemaker & Lynch 2022 | [원문] |
| 고전 자료 | 원생동물 운동성 (2장) | 수치 미열람 | Fenchel 1987, 『Ecology of Protozoa』, Springer, doi:10.1007/978-3-662-06817-5 | [미확인] |

c_rel 예시 [계산]: V = 10 µm³이면 21%, 100이면 4.7%, 1,000이면 1.0%, 10,000이면 0.23%.

게임 식 제안: 운동 기관 비용 = 건설 예산 × c_rel(V). 섬모는 대략 `2.8e9·V^0.86` ATP로 잡아 편모보다 큰 세포에서 효율적이다. 속도는 편모 약 4 체장/s, 섬모 약 6 체장/s.
트레이드오프: 작은 세포는 편모 비용 비중이 커서(V가 10 µm³ 이하이면 20% 이상) 운동보다 부유가 유리하다. 큰 세포는 섬모로 빠르게 움직이며 먹이를 만나는 비율과 흡수(Sh)가 늘어난다.

---

## 8. 세포 탄소·질소, 밀도, 퇴적물 공급

### 8.1 탄소·질소와 부피

Menden-Deuer & Lessard 2000, L&O 45:569–579, doi:10.4319/lo.2000.45.3.0569 [초록]:
- 원생생물 (규조 제외): `pgC = 0.216·V^0.939`
- 규조: `pgC = 0.288·V^0.811`
- 와편모조: `pgC = 0.760·V^0.819`, `pgN = 0.118·V^0.849`
- 자료 범위: 탄소 48 – 3.0×10⁴ pgC/cell, 부피 180 – 2.8×10⁵ µm³. 탄소 / 부피는 일정하지 않으므로 고정 환산 계수를 쓰면 계통적 오차가 생긴다.

| V (µm³) | 원생생물 pgC | 규조 pgC | 와편모조 pgC | 와편모조 pgN | 질량 C:N (와편모조) |
|---|---|---|---|---|---|
| 100 | 16 | 12 | 33 | 5.9 | 5.6 |
| 1,000 | 142 | 78 | 218 | 42 | 5.2 |
| 10,000 | 1,232 | 505 | 1,435 | 294 | 4.9 |

[계산] 몰 C:N은 약 6으로 Redfield 6.6과 비슷하다 (Redfield 값은 [미확인] 통념값).

### 8.2 개체 밀도 (cells/mL)

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 세균, HNAN | 담수 108곳(호수·연못·강·습지). 둘 다 생산성과 함께 증가하고 약 4자릿수에 걸쳐 상관 | 세균 3×10⁵–1×10⁹, HNAN 3×10²–4×10⁶ mL⁻¹ | Berninger, Finlay & Kuuppo-Leinikki 1991, L&O 36:139–147, doi:10.4319/lo.1991.36.1.0139 | [초록] (색인의 "respectively" 순서를 세균:HNAN 비 약 1000:1에 맞춰 해석함) |
| 비율 | 해양과 담수 유광층 모두 | 세균 약 1000 : HNAN 1 | Sanders, Caron & Berninger 1992, MEPS 86:1–14, doi:10.3354/meps086001 | [초록] |
| 지중해 얕은 호수 | 14개 호수 (TP 30–925 µg/L) | 세균 1–14×10⁶, 섬모충 0.6–229 mL⁻¹. 섬모충은 Chl a와만 상관 | Conty, García-Criado & Bécares 2007, Hydrobiologia 584:327–335, doi:10.1007/s10750-007-0585-x | [2차] (검색 요약 초록) |
| 섬모충과 영양 상태 | 섬모충 밀도와 바이오매스는 Chl a와 강하게 관련. 부영양화하면 큰 소모충류가 작은 스쿠티코섬모충으로 교체 | — | Beaver & Crisman 1989, Microb Ecol 17:111–136 | [초록] (검색 요약) |

게임 초기값 제안: 빈영양은 세균 10⁶, HNF 10³, 섬모충 1–3 mL⁻¹. 부영양은 세균 10⁷, HNF 10⁴, 섬모충 20–200 mL⁻¹.

### 8.3 퇴적물 영양염·산소 플럭스

| 항목 | 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|---|
| 무산소 P 방출 | 빈영양 Chub Lake와 부영양 호수 비교. 56개 호수 회귀에서 호수 P 농도와 양의 관계 | 빈영양 2.2, 부영양 약 14 mg P m⁻² d⁻¹ | Nürnberg 1986, CJFAS 43:574–580, doi:10.1139/f86-068 | [2차] (검색 요약 초록) |
| 방출 예측 | 7개 호수 코어 배양. 무산소 방출률은 퇴적물 총 P와 환원 용해성 P에 비례. 방출된 P는 생물이 거의 다 쓸 수 있음 | 수치는 초록에 없음 | Nürnberg 1988, CJFAS 45:453–462, doi:10.1139/f88-054 | [초록] |
| 산화 조건 | 빈영양 호수에서 P 방출률은 거의 0 | ≈ 0 | Aquatic Sciences 2020 (s00027-020-00724-0) | [2차] |
| 퇴적물 O2 수요 | 1.4절 참조 | 0.08 (빈영양) – 0.37 (환원물질) – 1.3 g O2 m⁻² d⁻¹ | Mathias & Barica 1980, Müller 2012 | [초록] |

게임 식 제안: 바닥 칸의 O2가 약 1–2 mg/L보다 크면 P 방출을 약 0으로 두고, 무산소이면 `P_flux = 2 ~ 15 mg P m⁻² d⁻¹ × (퇴적물 P 저장량 / 기준값)`. 퇴적물 P 저장량은 가라앉아 쌓인 유기물의 P로 채운다. 이렇게 하면 부영양화 → 무산소 → 내부 부하 → 더 심한 부영양화라는 양의 피드백이 저절로 생긴다.

---

## 9. 트레이드오프 요약 (게임 설계용)

| 형질 | 이득 | 비용 | 근거 |
|---|---|---|---|
| 작은 크기 | 부피당 흡수 증가 (r⁻²), Ks 낮음, 침강 느림 (∝ r²) | 포식 취약, 운동 기관 비용 비율이 큼 (c_rel ∝ V^−0.66) | Finkel 2010, Schavemaker 2022 |
| 길쭉함 / 가시 / 돌기 | Φ 증가 (침강 감소), 흡수 증가 (종횡비 10에서 +54%), 삼키기 어려움 | 표면적과 건설비 증가 | Padisák 2003, [계산], Pahlow 1997 |
| 군체 / 사슬 | 포식 회피 (크기 피난처), Φ 최대 8 | 유효 반지름 증가로 침강 증가, 세포당 확산 공급 감소 | Lürling & Van Donk 2000, Pahlow 1997 |
| 규산 각 | 강한 방어, 건설비는 유기 벽의 1/10 | 밀도 2.1–2.3으로 침강 증가, Si 필요 | Hendry 2018, Zhu 2026 |
| 가스 소포 | 떠올라 빛을 얻음, 하루 주기 부침 | 단백질 비용 (미확인), 붕괴압과 소포 효율의 상충 | Walsby 1994 |
| 편모 | 운동 (약 4 체장/s), 위치 유지 | 작은 세포에서 비싸다 (예산의 1–20%) | Lisicki 2019, Schavemaker 2022 |
| 섬모 | 빠름 (약 6 체장/s, 최대 7 mm/s), 큰 세포에서 효율적 | 절대 비용이 크다 (∝ V^0.86) | 같음 |
| 유도 방어 | 필요할 때만 켬, 수 시간 안에 발현 | 발현 중 성장 −15% | Kusch & Kuhlmann 1994 |
| 높은 Chl:C (약광 적응) | 약광에서 광합성 | 강광 광저해, 질소 비용, package effect (큰 세포) | Geider 1987, Finkel 2010 |

---

## 10. 미확인 목록 (쓸 때 주의)

1. Smayda 1970의 분류군별 침강 속도 범위. 원문을 못 봤으므로 Zhu et al. 2026 리뷰 값으로 대체했다.
2. Fenchel 1987의 유영 속도와 크기 수치. Lisicki et al. 2019 값으로 대체했다.
3. Hondzo & Stefan 1993의 계수 8.17×10⁻⁴·As^0.56. 1991 초록의 a ~10⁻⁴, b −0.36 ~ −0.45만 확인했다.
4. Fenchel & Finlay 1983 호흡식의 계수. 지수 0.75만 확인했다.
5. Laws 1991의 PQ 값 1.4와 1.1. 2차 인용이며, 다른 인용(1.3과 1.0)과 충돌한다.
6. Morris 1995의 Kd(320) 계수, Kd(305) = 2.76·DOC^1.23, Kd(PAR) 선형식 (모두 2차).
7. 가스 소포 자체 밀도(약 120 kg m⁻³)와 군체 밀도 975 / 1022 kg m⁻³의 출처.
8. Euplotes 방어형의 크기 변화(%). Lembadion이 유도하는 경우의 비용도 미확인이다.
9. Pančić & Kiørboe 2018의 방어 비용 수치. 초록에는 정성 결론뿐이다.
10. Gorham & Boyce 1989의 수온약층 식 지수(5.8·F^0.37, OCR 2차).
11. Chisholm 1992 장 본문, Karp-Boss 1996 본문. Sherwood 식은 Ploug 1999의 재인용이다.
12. 순수 물 Kw(PAR), 분자 열확산 1.4×10⁻⁷ m²/s, 영양염 확산계수 약 10⁻⁹ m²/s, 방해석 2.71 g cm⁻³, Redfield C:N, Poole-Atkins 1.7/SD는 모두 통념 물성으로 원문을 확인하지 않았다.
13. Ik의 멕시코 호수 논문 저자, Loiselle 2008의 권호.
14. Eppley 1972의 최대 성장식 μmax = 0.851·1.066^T doublings/d (Fishery Bull 70:1063–1085). Q10 약 1.88은 2차로 확인했고 계수는 미확인이다. 온도 의존이 필요하면 참고로 쓴다.

---

## 참고문헌

- Benson BB, Krause D (1984) The concentration and isotopic fractionation of oxygen dissolved in freshwater and seawater in equilibrium with the atmosphere. *Limnol Oceanogr* 29:620–632. doi:10.4319/lo.1984.29.3.0620
- Berninger U-G, Finlay BJ, Kuuppo-Leinikki P (1991) Protozoan control of bacterial abundances in freshwater. *Limnol Oceanogr* 36:139–147. doi:10.4319/lo.1991.36.1.0139
- Bright DI, Walsby AE (1999) The relationship between critical pressure and width of gas vesicles in isolates of *Planktothrix rubescens* from Lake Zürich. *Microbiology* 145:2769–2775. doi:10.1099/00221287-145-10-2769
- Chisholm SW (1992) Phytoplankton size. In: Falkowski PG, Woodhead AD (eds) *Primary Productivity and Biogeochemical Cycles in the Sea*. Plenum, pp 213–237. doi:10.1007/978-1-4899-0762-2
- Cole JJ, Caraco NF (1998) Atmospheric exchange of carbon dioxide in a low-wind oligotrophic lake measured by the addition of SF6. *Limnol Oceanogr* 43:647–656. doi:10.4319/lo.1998.43.4.0647
- Conty A, García-Criado F, Bécares E (2007) Changes in bacterial and ciliate densities with trophic status in Mediterranean shallow lakes. *Hydrobiologia* 584:327–335. doi:10.1007/s10750-007-0585-x
- Fenchel T (1987) *Ecology of Protozoa*. Springer. doi:10.1007/978-3-662-06817-5
- Fenchel T, Finlay BJ (1983) Respiration rates in heterotrophic, free-living protozoa. *Microb Ecol* 9:99–122. doi:10.1007/BF02015125
- Finkel ZV (2001) Light absorption and size scaling of light-limited metabolism in marine diatoms. *Limnol Oceanogr* 46:86–94. doi:10.4319/lo.2001.46.1.0086
- Finkel ZV, Beardall J, Flynn KJ, Quigg A, Rees TAV, Raven JA (2010) Phytoplankton in a changing world: cell size and elemental stoichiometry. *J Plankton Res* 32:119–137. doi:10.1093/plankt/fbp098
- Garcia HE, Gordon LI (1992) Oxygen solubility in seawater: Better fitting equations. *Limnol Oceanogr* 37:1307–1312. doi:10.4319/lo.1992.37.6.1307
- Geider RJ (1987) Light and temperature dependence of the carbon to chlorophyll a ratio in microalgae and cyanobacteria. *New Phytol* 106:1–34. doi:10.1111/j.1469-8137.1987.tb04788.x
- Gorham E, Boyce FM (1989) Influence of lake surface area and depth upon thermal stratification and the depth of the summer thermocline. *J Great Lakes Res* 15:233–245.
- Hendry KR et al. (2018) Competition between silicifiers and non-silicifiers in the past and present ocean and its evolutionary impacts. *Front Mar Sci* 5:22. doi:10.3389/fmars.2018.00022
- Hondzo M, Ellis CR, Stefan HG (1991) Vertical diffusion in small stratified lake: data and error analysis. *J Hydraul Eng* 117:1352–1369. doi:10.1061/(ASCE)0733-9429(1991)117:10(1352)
- Hondzo M, Stefan HG (1993) Lake water temperature simulation model. *J Hydraul Eng* 119:1251–1273.
- Kammerlander B et al. (2016) Ciliate community structure and interactions within the planktonic food web in two alpine lakes of contrasting transparency. *Freshw Biol* 61:1950–1965. doi:10.1111/fwb.12828
- Karp-Boss L, Boss E, Jumars PA (1996) Nutrient fluxes to planktonic osmotrophs in the presence of fluid motion. *Oceanogr Mar Biol Annu Rev* 34:71–107.
- Kell GS (1975) Density, thermal expansivity, and compressibility of liquid water from 0° to 150 °C. *J Chem Eng Data* 20:97–105. (식만 사용, 표준값과 대조 [계산])
- Kopp M, Tollrian R (2003) Trophic size polyphenism in *Lembadion bullinum*: costs and benefits of an inducible offense. *Ecology* 84:641–651.
- Kuhlmann H-W, Heckmann K (1985) Interspecific morphogens regulating prey–predator relationships in protozoa. *Science* 227:1347–1349. doi:10.1126/science.227.4692.1347
- Kusch J, Kuhlmann H-W (1994) Cost of *Stenostomum*-induced morphological defence in the ciliate *Euplotes octocarinatus*. *Arch Hydrobiol* 130:257–267. doi:10.1127/archiv-hydrobiol/130/1994/257
- Laws EA (1991) Photosynthetic quotients, new production and net community production in the open ocean. *Deep-Sea Res A* 38:143–167. doi:10.1016/0198-0149(91)90059-O
- Lewis WM Jr (1983) A revised classification of lakes based on mixing. *Can J Fish Aquat Sci* 40:1779–1787. doi:10.1139/f83-207
- Lisicki M, Rodrigues MFV, Goldstein RE, Lauga E (2019) Swimming eukaryotic microorganisms exhibit a universal speed distribution. *eLife* 8:e44907. doi:10.7554/eLife.44907
- Lürling M (2003) Phenotypic plasticity in the green algae *Desmodesmus* and *Scenedesmus* with special reference to the induction of defensive morphology. *Ann Limnol* 39:85–101. doi:10.1051/limn/2003014
- Lürling M, Van Donk E (2000) Grazer-induced colony formation in *Scenedesmus*: are there costs to being colonial? *Oikos* 88:111–118. doi:10.1034/j.1600-0706.2000.880113.x
- Ma J, Song K, Wen Z, et al. (2016) Spatial distribution of diffuse attenuation of photosynthetic active radiation and its main regulating factors in inland waters of Northeast China. *Remote Sens* 8:964. doi:10.3390/rs8110964
- MacIntyre HL, Kana TM, Anning T, Geider RJ (2002) Photoacclimation of photosynthesis irradiance response curves and photosynthetic pigments in microalgae and cyanobacteria. *J Phycol* 38:17–38. doi:10.1046/j.1529-8817.2002.00094.x
- Markager S, Vincent WF, Tang EPY (1999) Carbon fixation by phytoplankton in high Arctic lakes: implications of low temperature for photosynthesis. *Limnol Oceanogr* 44:597–607. doi:10.4319/lo.1999.44.3.0597
- Mathias JA, Barica J (1980) Factors controlling oxygen depletion in ice-covered lakes. *Can J Fish Aquat Sci* 37:185–194. doi:10.1139/f80-024
- Menden-Deuer S, Lessard EJ (2000) Carbon to volume relationships for dinoflagellates, diatoms, and other protist plankton. *Limnol Oceanogr* 45:569–579. doi:10.4319/lo.2000.45.3.0569
- Morris DP, Zagarese H, Williamson CE, et al. (1995) The attenuation of solar UV radiation in lakes and the role of dissolved organic carbon. *Limnol Oceanogr* 40:1381–1391. doi:10.4319/lo.1995.40.8.1381
- Müller B, Bryant LD, Matzinger A, Wüest A (2012) Hypolimnetic oxygen depletion in eutrophic lakes. *Environ Sci Technol* 46:9964–9971. doi:10.1021/es301422r
- Müller B, Steinsberger T, Schwefel R, et al. (2019) Oxygen consumption in seasonally stratified lakes decreases only below a marginal phosphorus threshold. *Sci Rep* 9:18054. doi:10.1038/s41598-019-54486-3
- Nürnberg GK (1986) Internal phosphorus load in an oligotrophic Precambrian Shield lake with an anoxic hypolimnion. *Can J Fish Aquat Sci* 43:574–580. doi:10.1139/f86-068
- Nürnberg GK (1988) Prediction of phosphorus release rates from total and reductant-soluble phosphorus in anoxic lake sediments. *Can J Fish Aquat Sci* 45:453–462. doi:10.1139/f88-054
- Padisák J, Soróczki-Pintér É, Rezner Z (2003) Sinking properties of some phytoplankton shapes and the relation of form resistance to morphological diversity of plankton – an experimental study. *Hydrobiologia* 500:243–257. doi:10.1023/A:1024613001147
- Pahlow M, Riebesell U, Wolf-Gladrow DA (1997) Impact of cell shape and chain formation on nutrient acquisition by marine diatoms. *Limnol Oceanogr* 42:1660–1672. doi:10.4319/lo.1997.42.8.1660
- Pančić M, Kiørboe T (2018) Phytoplankton defence mechanisms: traits and trade-offs. *Biol Rev* 93:1269–1303. doi:10.1111/brv.12395
- Raymond PA, Zappa CJ, Butman D, et al. (2012) Scaling the gas transfer velocity and hydraulic geometry in streams and small rivers. *Limnol Oceanogr Fluids Environ* 2:41–53. doi:10.1215/21573689-1597669
- Richardson K, Beardall J, Raven JA (1983) Adaptation of unicellular algae to irradiance: an analysis of strategies. *New Phytol* 93:157–191. doi:10.1111/j.1469-8137.1983.tb03422.x
- Rippey B, McSorley C (2009) Oxygen depletion in lake hypolimnia. *Limnol Oceanogr* 54:905–916. doi:10.4319/lo.2009.54.3.0905
- Salas de León DA, Alcocer J, Ardiles Gloria V, Quiroz-Martínez B (2016) Estimation of the eddy diffusivity coefficient in a warm monomictic tropical lake. *J Limnol* 75(s1):161–168. doi:10.4081/jlimnol.2016.1431
- Sanders RW, Caron DA, Berninger U-G (1992) Relationships between bacteria and heterotrophic nanoplankton in marine and fresh waters: an inter-ecosystem comparison. *Mar Ecol Prog Ser* 86:1–14. doi:10.3354/meps086001
- Schavemaker PE, Lynch M (2022) Flagellar energy costs across the tree of life. *eLife* 11:e77266. doi:10.7554/eLife.77266
- Smayda TJ (1970) The suspension and sinking of phytoplankton in the sea. *Oceanogr Mar Biol Annu Rev* 8:353–414.
- Steinsberger T, Schwefel R, Wüest A, Müller B (2020) Hypolimnetic oxygen depletion rates in deep lakes: effects of trophic state and organic matter accumulation. *Limnol Oceanogr*. doi:10.1002/lno.11578
- Trentman MT, Hall RO Jr, Valett HM (2023) Exploring the mismatch between the theory and application of photosynthetic quotients in aquatic ecosystems. *Limnol Oceanogr Lett* 8:565–579. doi:10.1002/lol2.10326
- Walsby AE (1994) Gas vesicles. *Microbiol Rev* 58:94–144. doi:10.1128/mr.58.1.94-144.1994
- Walsby AE, Hayes PK, Boje R (1995) The gas vesicles, buoyancy and vertical distribution of cyanobacteria in the Baltic Sea. *Eur J Phycol* 30:87–94. doi:10.1080/09670269500650851
- Wüest A, Lorke A (2003) Small-scale hydrodynamics in lakes. *Annu Rev Fluid Mech* 35:373–412. doi:10.1146/annurev.fluid.35.101101.161220
- Zhu J, Cheng J, Zeng J, et al. (2026) A review of phytoplankton sinking rates: mechanisms, methodologies, and biogeochemical implications. *Biology* 15:130. doi:10.3390/biology15020130
- Zwart JA 외 (2015) *Ecology* 96, ESA Ecological Archives E096-197 Appendix A (esapubs.org/archive/ecol/E096/197/appendix-A.pdf). 부록만 읽었다. 본문 제목과 공저자는 [미확인]
