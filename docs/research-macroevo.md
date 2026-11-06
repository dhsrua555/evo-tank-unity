# 원생생물 → 어류: 진화 경로와 환경 조건 연구 근거

작성 2026-11-06. 대상 게임: 계통 x 공간 칸의 개체 수를 확률적으로 계산하고, 핵심 혁신을 유전자 문턱으로 여는 수조 시뮬레이션.

## 읽는 법

- **확인**: 1차 논문(또는 리뷰)의 초록·본문을 직접 읽어 수치를 대조했다(PubMed E-utilities, OpenAlex, Nature/PMC 페이지, 원문 PDF).
- **부분**: 2차 출처(보도자료, 위키, 검색 요약)로만 확인했거나, 일부 수치만 원문에서 확인했다.
- **미확인**: 원문을 찾지 못했다. 게임에 쓰려면 추가 확인이 필요하다.
- **계산**: 원문 수치를 내가 단위 환산하거나 표준 식으로 계산한 값이다(원문 주장 아님).
- **설계 제안**: 문헌 수치에서 게임용으로 뽑은 값이다. 문헌 사실과 섞지 않도록 7장에 따로 모았다.

단위 환산(계산): 1 PAL(현재 대기 산소) = 0.21 atm ≈ 21.2 kPa. 용존 산소 1 mg/L = 31.25 µmol/L. 1 mL/L = 1.43 mg/L = 44.7 µmol/L. Breitburg 2018은 2 mg/L = 63 µmol/L ≈ 61 µmol/kg로 적는다(확인). Sperling 2013은 0.2 mL/L ≈ 9 µmol/kg ≈ 0.29 mg/L로 적는다(확인). 표층수는 대기와 평형하므로(헨리 법칙) x% PAL이면 같은 수온·염분의 포화 용존 산소도 약 x%다.

---

## 0. 한눈에 보는 핵심 문턱

| # | 변수 | 값 | 출처 | 확인 |
|---|---|---|---|---|
| 1 | 해면(초기 동물 대용) 생존 최소 O2 | 0.5–4.0% PAL | Mills 2014 PNAS | 확인 |
| 2 | 호기 대사 하한 | 1–2% PAL 아래에서는 호기 대사 불가 | Payne 2009 PNAS 본문 | 확인 |
| 3 | 확산만으로 사는 몸 크기 / 순환계 필요 크기 | pO2 ≳ 10^3 Pa(≈5% PAL)면 약 1 mm. pO2 10^3–10^4 Pa(≈5–50% PAL)면 순환계를 가진 약 1 cm 이상의 복잡 생물이 가능 | Catling 2005 Astrobiology | 확인(초록. 지수의 음수 부호는 문맥으로 판독) |
| 4 | 육식(포식자)이 사라지는 바닥 O2 | 0.34 mL/L(≈15 µM, ≈0.49 mg/L) 미만에서만 포식 다모류가 완전히 사라짐. 아산소(0–0.2 mL/L) 정점의 절반은 포식자 0 | Sperling 2013 PNAS | 확인 |
| 5 | 캄브리아 동물 최소 O2 | > 약 0.1 PAL | Dahl 2010 PNAS 본문 | 확인 |
| 6 | 1 m 넘는 포식 어류 | 표층 O2 > 약 100 µM ≈ 40% PAL 필요(Dahl 추정). 반례: 실루리아기 423 Ma에 약 1 m 어류(Megamastax) | Dahl 2010; Choo 2014 Sci Rep | 확인 |
| 7 | 몸 크기 최대치의 2단계 도약 | 약 1.9 Ga(진핵세포)와 0.6–0.45 Ga(다세포)에 각각 약 6자릿수. 오르도비스기 이후 동물은 1.5자릿수만 증가 | Payne 2009 PNAS | 확인 |
| 8 | 오존 UV 차폐 | 고전적으로 "1% PAL이면 충분". 3D 모델로는 5–10% PAL이 필요(1% PAL 66 DU, 10% PAL 169 DU, 산업화 이전 279 DU) | Cooke 2022 R Soc Open Sci | 확인 |
| 9 | 어류 저산소 치사 / 아치사 | 어류 LC50 평균 1.54 mg/L, 아치사 SLC50 평균 4.41 mg/L. 전 분류군 LC50의 90%가 4.59 mg/L 미만 → 안전 기준 4.6 mg/L | Vaquer-Sunyer & Duarte 2008 PNAS | 확인 |
| 10 | 저산소의 관행 정의 | ≤ 2 mg/L(63 µM). 1950년 이후 연안 500곳 넘게 보고 | Breitburg 2018 Science | 확인 |
| 11 | 대사 지수(온도 x 산소) | Φ = O2 공급 / 휴식 대사 수요. 종의 적도 쪽 분포 끝은 Φcrit ≈ 2–5 | Deutsch 2015 Science | 확인 |
| 12 | 어류 온도 범위 | 남극어 상한 치사 6 °C(가장 낮은 기록). 마가디 틸라피아 CTmax 45.6 °C(가장 높은 기록) | Somero & DeVries 1967; Wood 2016 | 확인 |
| 13 | 수온 → 몸 크기(수생) | 건중량 약 100 mg 종에서 -5.0%/°C(육상 -0.5%/°C) | Forster 2012 PNAS | 확인 |
| 14 | 삼투 조절 비용 | 대사의 "몇 %에서 30% 넘게"까지(종마다 다름). 옛 추정 20–50% 이상, 최근 추정 약 10%. 중간 염분(8–20 psu)에서 성장이 좋은 경우가 많음 | Ern 2014 J Fish Biol; Boeuf & Payan 2001 | 확인 |
| 15 | 광염성 비율·염분 상한 | 광염성은 소수(< 10%). 광염성 어류 대부분 상한 약 60 g/kg(해수의 2배), 일부는 130 g/kg | Schultz & McCormick 2013; Kültz 2015 JEB | 확인 |
| 16 | 다세포가 생기는 세대 수 | 포식자를 넣으면 클로렐라가 100세대 안에 군체화(8세포 군체가 우세). 클라미도모나스는 약 750세대 안에 5개 집단 중 2개가 다세포 | Boraas 1998; Herron 2019 Sci Rep | Boraas 부분, Herron 확인 |
| 17 | 눈 진화 세대 수 | 빛 감지 막 → 렌즈 눈: 약 1,829단계(단계마다 1%), 약 36만 4천 세대 | Nilsson & Pelger 1994 | 부분(초록은 "몇십만 년"만. 세부 수치는 2차 출처) |
| 18 | 뇌 에너지 비용 | 척추동물 뇌는 휴식 시 O2 소비의 2–8%(사람 20%, 코끼리주둥이고기 약 60%) | Nilsson 1996 JEB | 확인 |

---

## 1. 동물 다세포성의 기원

### 1.1 주장·수치 표

| 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|
| 깃편모충 *Salpingoeca rosetta*는 먹이 세균 *Algoriphagus machipongonensis*(박테로이데테스)가 있으면 로제트 군체를 만든다. 신호 물질 RIF-1은 설포리피드 | 펨토몰(10^-15 M)에서 작동하고, 반응 범위가 9자릿수 | Alegado 2012 eLife | 확인 |
| 같은 세균이 만드는 지질 세 종류가 로제트 발생을 켜고, 증폭하고, 억제한다(LPE는 혼자서는 효과 없지만 RIF와 시너지) | - | Woznica 2016 PNAS | 확인 |
| *S. rosetta* 로제트는 세포가 모여서(응집) 생기지 않고, 분열한 자매세포가 붙어 있어서 생긴다 → 동물 배 발생(접합자의 분열)과 같은 방식 | - | Fairclough, Dayel & King 2010 Curr Biol | 확인 |
| 컵 모양 군체 깃편모충 *Choanoeca flexa*는 빛 변화에 반응해(로돕신-cGMP 경로) 군체 곡률을 뒤집는다. 먹이 섭취 모드와 헤엄 모드를 오간다 | 감각세포·근육세포가 따로 없어도 군체 전체가 수축 | Brunet 2019 Science | 확인 |
| 군체의 먹이 이득에 대한 반대 결과 | 군체는 단세포보다 느리게 헤엄치지만, 세포 사이 유체 협력으로 군체에 공급되는 물이 늘어난다(이득 있음) | Roper 2013 Phys Rev Lett | 확인 |
| | 균질한 환경에서 여과섭식이 가장 잘 되는 것은 빨리 헤엄치는 단세포다. 강한 외부 흐름에서는 고착 세포가 유리하다. 군체의 이득은 섭식 말고 다른 데(포식 회피 등) 있을 수 있다 | Kirkegaard & Goldstein 2016 Phys Rev E | 확인 |
| 포식 압력 → 군체: 단세포 클로렐라 연속배양에 식세포성 편모충 *Ochromonas vallescia*를 넣었다 | 먹이 기준 100세대 안에 다세포형이 우세해졌다. 처음엔 수십~수백 세포 덩어리, 10–20세대 뒤엔 8세포 군체가 우세했고, 8세포 형태가 계속 유지됐다. 포식에 거의 면역이면서도 세포마다 배지에 닿는 크기였다 | Boraas, Seale & Boxhorn 1998 Evol Ecol | 부분(초록 내용은 검색 요약으로 확인. 원문 페이지는 접근 차단) |
| 같은 계(클로렐라 + 섬모충 *Tetrahymena*)의 반대 측면 | 포식자 또는 포식자 분비물만 있어도 군체가 생기고, 포식자가 많을수록 군체도 많다. 포식자를 없애면 단세포로 돌아간다 → 진화라기보다 표현형 가소성 | Fisher, Bell & West 2016 J Evol Biol | 확인 |
| *Chlamydomonas reinhardtii*를 여과섭식 포식자 *Paramecium tetraurelia*로 선택했다 | 약 750 무성 세대 안에 5개 집단 중 2개가 다세포 구조를 진화시켰다. 세포 수와 번식체 크기는 분리주마다 다르다. 다세포가 실제로 포식을 막는다 | Herron 2019 Sci Rep | 확인 |
| 효모를 빠른 침강으로 선택하면 '눈송이' 다세포가 빠르게 진화한다. 단일 클론, 번식체, 유년기, 정해진 성장, 세포자멸 증가(분업) | 초록에는 "빠르게"라고만 적혀 있다(약 60일 등 구체 기간은 원문 미대조) | Ratcliff 2012 PNAS | 확인(기간은 미확인) |
| 영양염과 다세포: 효모는 분비 효소(인버테이스)로 자당을 분해하는 '공공재'를 쓴다. 붙어 있는 덩어리는 단세포가 못 자라는 조건에서 자라고, 사기꾼 세포와의 경쟁에서 이긴다 | 낮은 자당 농도에서 덩어리가 유리 | Koschwanez, Foster & Murray 2011 PLoS Biol | 확인 |

### 1.2 볼복스류: 크기와 세포 분화

| 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|
| 단세포 *Chlamydomonas* → *Gonium*(작은 군체, 방향성 유영) → *Pandorina*, *Eudorina*, *Pleodorina*(커지면서 두 세포형으로 분화하는 경향) → *Volvox carteri*(세포 수천 개, 생식-체세포 완전 분업). 단세포 조상에서 12단계의 발생 변화 | Kirk는 공통 조상을 50 ± 20 Ma로 추정 | Kirk 2005 BioEssays | 확인 |
| 연대 반론: 볼복스는 최소 2억 년 전에 단세포 조상에서 갈라졌다 | ≥ 200 Ma | Herron 2009 PNAS | 확인 |
| 화석 보정 분자시계: 다세포성은 볼복스류에서 두 번 독립적으로 생겼다(Goniaceae + Volvocaceae는 석탄기–트라이아스기). 분화로 가는 순서는 Kirk가 제안한 그대로였다 | - | Lindsey, Knoll, Herron & Rosenzweig 2024 BMC Biol | 확인 |
| 군체가 커질수록 떠 있고 움직이려면 체세포(편모 세포) 비율이 늘어야 한다. 편모가 없는 생식세포를 안쪽에 넣으면 항력이 줄어든다. 유체역학으로 구한 최대 크기가 실제 가장 큰 종의 크기와 맞는다 | 크기가 커지는 비용(침강·운동)을 세포 분화가 상쇄한다 | Solari, Kessler & Michod 2006 Am Nat | 확인 |
| 큰 군체에서는 확산만으로 대사물질 교환이 부족하다. 편모가 만드는 흐름(이류)이 필수다 | 페클레 수 Pe = 2UR/D ≫ 1 | Solari 2006 PNAS | 확인 |

### 1.3 게임 해석: 다세포가 주는 것과 잃는 것

- **얻는 것**: 포식자 입에 안 들어가는 크기(Boraas, Herron), 공공재를 같이 쓰는 효율(Koschwanez), 군체 유체 공급(Roper), 분업 시작(Ratcliff).
- **잃는 것**: 유영 속도·섭식 효율(Roper 2013도 군체가 느리다고 인정. Kirkegaard 2016은 단세포가 여과섭식 최적), 침강(Solari), 내부 확산 한계(Solari 2006 PNAS).
- **유발 요인**: 포식 압력, 세균 신호, 영양 공공재. 산소는 직접 요인이 아니다(아래 Mills 2014 참조).
- **가역성**: 포식자를 없애면 단세포로 되돌아간다(Fisher 2016) → 첫 문턱은 '가소성', 둘째 문턱(유전 고정)은 수백 세대 지속(Herron 2019 약 750세대)으로 나누면 문헌과 맞는다.

---

## 2. 산소 문턱과 대기·해양 산소 역사

### 2.1 동물 생리의 산소 요구

| 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|
| 현생 해면(*Halichondria panicea*)은 저산소에서 산다 → 동물의 기원은 산소 증가가 방아쇠가 아니었을 수 있다. 다른 생태·발생 요인이 필요하다 | 0.5–4.0% PAL | Mills 2014 PNAS | 확인 |
| 같은 호 해설: 산소와 동물 역사 | - | Knoll & Sperling 2014 PNAS | 확인(제목·저자만) |
| 현대 산소 최소층을 원생대 바다의 대용으로 쓰면, 낮은 산소 → 포식자 비율과 포식 분류군 다양성이 낮다. 산소가 높을수록 먹이그물이 복잡하다 → "산소가 육식을 통제"라는 생리 연결이 환경 방아쇠(산소)와 생태 동력(군비경쟁)을 잇는다 | 포식 다모류는 < 0.34 mL/L(≈15 µM)에서만 완전히 사라진다. 아산소 0–0.2, 심한 저산소 0.2–0.5, 중간 0.5–1.0, 약한 저산소 1.0–2.0 mL/L 구간으로 분석. 아산소 정점의 절반은 포식자 0. 원생대의 PAL 값은 제시하지 않음 | Sperling 2013 PNAS | 확인 |
| 산소는 최대 몸 크기와 직결 | 정성적 서술만 | Sperling 2013 | 확인 |
| 산소는 질량·부피가 아니라 '두께(표면적 대비 부피)'를 제한한다 → 대사율과 산소만으로는 최대 크기를 예측할 수 없다. 실험 저산소는 동물 크기를 줄인다 | 정성적 | Payne 2011 Photosynth Res(리뷰) | 확인 |
| 확산 의존 생물 vs 순환계 생물의 산소 문턱 | pO2 > 약 10^3 Pa면 약 10^-3 m(확산 의존). 10^3–10^4 Pa면 약 10^-2 m 문턱을 넘는 순환계 생물. 호기 대사는 같은 먹이에서 혐기보다 약 10배의 에너지 → 혐기 생물은 단열 세포 사슬 이상 복잡해지지 않는다. 생물량 누적 밀도 n(>m) ∝ m^-1(호기), 혐기는 ∝ m^-1.5로 예측 | Catling 2005 Astrobiology | 확인(초록. 10^-3·10^-2의 음수 부호는 PubMed 텍스트에서 빠져 문맥으로 판독) |
| 극지 거대화: 저서 단각류 1,853종, 12곳 분석 → 최대 잠재 크기는 산소 가용성이 제한 | 차가운 물일수록 O2가 많아 더 큼 | Chapelle & Peck 1999 Nature | 확인 |
| 디킨소니아: 원통형 마디 + 콜라겐 벽이면 확산만으로 산소 공급 가능(정육면체 마디는 불가). 몸 지름 약 50 mm에서 A/V 비가 하한에 가까워짐 | - | Gooden 2014 J Proc R Soc NSW | 확인(초록) |

### 2.2 대기·해양 산소 역사(게임의 '시대별 O2' 곡선 근거)

| 시기 | 산소 수준 | 출처 | 확인 / 논쟁 |
|---|---|---|---|
| 약 2.4–2.3 Ga | 대기에 처음 O2 축적(대산화 사건) | Lyons 2021 Astrobiology; Lyons 2014 Nature(리뷰) | 확인 |
| 1.8–0.8 Ga(중기 원생대) | 최대 0.1% PAL(크롬 동위원소) | Planavsky 2014 Science | 확인 |
| 1.4 Ga | 반대 결과: 대기 O2 > 4% PAL(샤마링층 미량금속 + 모델) → 동물 호흡에 충분한 산소가 동물 등장보다 훨씬 먼저 있었다 | Zhang 2016 PNAS | 확인(PNAS에 논평·반박 서신 있음) |
| 약 0.8 Ga | 산소 상승 시작, 복잡 생명 발달과 겹침. 인과 방향은 아직 논쟁 | Lyons 2021 | 확인 |
| 후기 원생대 상승 | "현재의 몇 %" → 약 15–50% PAL(Dahl의 종합). 초기 고생대 모델: COPSE 20–60% PAL, GEOCARB 거의 현재 수준 | Dahl 2010 본문 | 확인(본문 의역 기준) |
| 550–560 Ma | 1차 해양 산소화(Mo 동위원소). 에디아카라 동물군·큰 좌우대칭 동물과 겹침 | Dahl 2010 | 확인 |
| 2,300–360 Ma 철 종분화 자료 4,700개 | 반대 결과: 심해는 원생대 내내 무산소·철 우세였고, 에디아카라–캄브리아 사이 통계적으로 유의한 산소 변화가 없다 → 신원생대 상승 폭은 작다. 산소화는 고생대까지 이어졌을 것. 동물 다양화에 산소가 작용했다면 '작은 증가가 기능·생태 문턱을 넘긴' 방식 | Sperling 2015 Nature | 확인 |
| 약 400 Ma(데본기) | 2차, 아마 더 큰 산소화. 관다발식물 다양화(유기탄소 매몰)와 큰 포식 어류 방산과 겹침. 해수 δ98Mo 약 1.1‰(원생대) → 약 1.4‰(후기 에디아카라) → 2.0‰(430–390 Ma, 현재 2.3‰) | Dahl 2010 | 확인. 반론: Butterfield 2011 PNAS 서신 "데본기 큰 포식 어류의 방산이 대기 산소 증가의 결과였나?"(제목만 확인), Choo 2014(실루리아기 1 m 어류) |
| 인과 뒤집기 | 큰 진핵 입자는 빨리 가라앉고 저서 여과섭식이 생기면서 산소 수요가 깊은 곳으로 옮겨 가 심층이 산소화됐다. 바닥 무산소가 줄면 퇴적물에서 인 방출이 줄고 → 생산성 감소 → 산소 수요 감소 → 추가 산소화(양의 되먹임) | Lenton 2014 Nat Geosci | 확인 |
| 대안 시각 | 산소·동물·해양 환기에 대한 대안 해석(동물이 해양 환기를 바꿨다는 쪽) | Butterfield 2009 Geobiology | 미확인(초록 없음. 제목·서지만 확인) |

### 2.3 몸 크기 – 산소: 2단계 증가

| 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|
| 생명 등장 이후 최대 크기는 16자릿수 증가했다. 그 약 75%가 두 번의 계단에서 일어났다 | 약 1.9 Ga 약 6자릿수(진핵세포), 0.6–0.45 Ga 약 6자릿수(진핵 다세포). 오르도비스기 두족류는 에디아카라 이전 최대 화석보다 거의 6자릿수 크다. 오르도비스기 이후 동물은 1.5자릿수만 증가 | Payne 2009 PNAS | 확인 |
| 각 계단은 산소 증가와 동시이거나 약간 뒤 → "환경 제한이 풀리자 잠재력이 실현됐다" | 캄브리아 시작에 산소 ≥ 10% PAL(지화학 근거 인용) | Payne 2009 | 확인 |
| 해양 동물 17,208속, 542 Ma 동안: 평균 생체부피 150배, 최대 10만 배 이상 증가, 최소는 10배 미만 감소. 중립 표류로 설명 안 되고, 대부분은 강(class) 사이의 차등 다양화 때문 | - | Heim 2015 Science | 확인 |

### 2.4 게임 해석

- 해면급 몸(분화 세포, 여과섭식)은 아주 낮은 O2(0.5–4% PAL)로 충분하다. "산소가 동물을 낳았다"는 오해를 피하려면, 다세포·세포 분화 문턱에는 O2 조건을 낮게 잡는 편이 문헌과 맞다.
- 산소가 크게 작용하는 혁신은 **두께·크기**(순환계 이전 확산 한계)와 **활발한 육식**(Sperling 2013)이다. 포식 계통의 생존 확률에 O2 항을 넣는 근거가 된다.
- 논쟁: 신원생대 산소 증가의 크기와 시기(Planavsky 2014 vs Zhang 2016, Sperling 2015). 시나리오 모드라면 'O2 곡선 프리셋' 2–3개로 두는 게 정직하다.

---

## 3. 몸 설계 혁신의 순서·연대·크기

### 3.1 연대표

| 단계(게임 혁신) | 최초 증거 | 연대 | 크기 | 출처 | 확인 |
|---|---|---|---|---|---|
| 동물 다세포(분자시계) | 크라운 후생동물은 크라이오기에 기원. 분자시계로 동물 다세포성은 약 800 Ma(토니아기), 좌우대칭 몸 설계는 약 650 Ma, 자매 문(phylum) 분기는 약 560–540 Ma | 약 800 / 650 / 560–540 Ma | - | Sperling & Stockey 2018 ICB; dos Reis 2015 Curr Biol | 확인. dos Reis는 "현재 자료로는 지질 사건과 상관을 검증할 만큼 정밀하지 않다"고 경고 |
| 해면 후보 체화석 | 케라토사 해면의 vermiform microstructure와 같은 구조가 약 890 Ma 초에서 발견. 신원생대 산소화 사건 이전·빙하기 이전 | 약 890 Ma | 밀리미터–센티미터 | Turner 2021 Nature | 확인("해면이라면"이라는 조건부) |
| 해면 생체지표 | 24-isopropylcholestane(데모스펀지 스테로이드)이 마리노아 빙하기 이전부터 → 약 1억 년 연속 기록 | ≥ 635 Ma | - | Love 2009 Nature | 확인 |
| 반론 | 이 C30 스테란은 해면 진단 지표가 아니며, 녹조류 C29 스테롤이 지질학적으로 메틸화돼 생길 수 있다 → 가장 오래된 동물 증거는 최후기 에디아카라로 되돌아간다 | - | - | Bobrovskiy 2021 Nat Ecol Evol | 확인 |
| 에디아카라 생물군 | 최초의 큰 복잡 생물. 디킨소니아는 콜레스테로이드만 만든다 → 동물로 확정 | 571–541 Ma | 디킨소니아 최대 약 1.4 m | Bobrovskiy 2018 Science(연대·동물 판정); 크기는 기네스 기록 페이지 | 연대·판정 확인, 크기 부분 |
| 근육(자포동물 후보) | *Haootia quadriformis*: 꼬인 섬유 다발이 사각형으로 배열 → 근육을 가진 자포동물(십자해파리류와 비슷) | 약 560 Ma | - | Liu 2014 Proc R Soc B | 확인 |
| 좌우대칭(가장 작은 쪽) | *Ikaria wariootia*: 앞뒤 구분이 있는 작고 단순한 좌우대칭 동물. 흔적화석 *Helminthoidichnites*를 만든 동물로 추정 | 560–551 Ma(방사성 연대 없음, 생층서 대비) | 길이 1.9–6.7 mm(최대 약 7 mm), 폭 1.1–2.4 mm. 15 mm보다 얇은 모래층에만 흔적 → 깊은 모래는 황화물 환경이라 산소 많은 얇은 층 선호 가능 | Evans 2020 PNAS | 확인 |
| 좌우대칭(연체동물형) | *Kimberella*: 광물화되지 않은 외투(껍데기)를 가진 저서 좌우대칭 동물. 큰 삼배엽 동물이 선캄브리아에 있었다는 증거 | ≥ 555.3 ± 0.3 Ma(백해 지역, U-Pb) | 큰 개체 약 15 cm | Fedonkin & Waggoner 1997 Nature; Martin 2000 Science; 크기는 2차 출처 | 형태·연대 확인, 크기 부분 |
| 포식의 첫 흔적 | 최초의 광물 골격 동물 *Cloudina*의 관에 구멍. 500개 넘는 관 중 2.7%에 지름 40–400 µm 구멍. 구멍 크기와 관 폭이 비례 → 크기를 골라 먹는 포식자 | 최후기 에디아카라(약 550 Ma) | - | Bengtson & Zhao 1992 Science | 확인 |
| | 중국 다른 산지: 관 약 100개 중 약 1/5에 폭 15–85 µm 포식 구멍. 같이 사는 *Sinotubulites*는 0% → 먹이 선택성, 방어 | - | - | Hua, Pratt & Zhang 2003 Palaios | 확인 |
| 캄브리아 시작 | 흔적화석 *Treptichnus pedum* 첫 출현 | 538.8 Ma | - | ICS 차트(위키피디아 경유) | 부분 |
| 캄브리아 폭발 주 단계 | 청장 생물군 최대 연대 518.03 ± 0.71 Ma → 폭발은 22 Myr 넘게 이어진 과정 | 약 518 Ma | - | Yang 2018 J Geol Soc | 확인 |
| 척삭동물(줄기) | *Pikaia gracilens*(표본 114개): 방추형, 근절 약 100개, 작은 두 갈래 머리 + 촉수 2개, 눈 없음, 지느러미살 없는 등지느러미, 인두 구멍 가능성, 배쪽 혈관(순환계 증거). '등 기관'은 척삭으로 보기 어렵다. 척삭 판정은 S자 근절에 주로 기댄다 | 약 505 Ma(버제스 셰일) | 평균 약 5 cm | Conway Morris & Caron 2012 Biol Rev; 크기는 ROM 보도자료 | 형태 확인, 크기 부분 |
| 최초 척추동물(무악어류) | 청장에서 무악어류 두 종: *Myllokunmingia*(칠성장어형에 가까움), *Haikouichthys*(먹장어형에 가까움). 확실한 무악어류 기록을 오르도비스기(약 475 Ma)에서 캄브리아 전기로 끌어내림 | 약 518 Ma | *Myllokunmingia* 28 mm(높이 6 mm), *Haikouichthys* 약 2.5 cm. Dahl 2010: 청장 어류 약 2.5–3.0 cm | Shu 1999 Nature; Yang 2018(연대); 크기는 위키(원문 Shu 1999 인용) + Dahl 2010 | 형태·연대 확인, 개별 크기 부분 |
| 머리·척추 | *Haikouichthys* 표본 다수: 눈, 비강 가능성, 이포(otic capsule) 가능성, 척삭 + 분리된 척추 요소 → 줄기 두개류 | 약 518 Ma | - | Shu 2003 Nature | 확인 |
| 턱(이빨) | *Qianodus duplicis* 이빨 소용돌이: 턱 있는 척추동물의 가장 오래된 직접 증거. 이빨 기록을 14 Myr 앞당김 | 약 439 Ma(실루리아기 전기) | - | Andreev 2022 Nature | 확인 |
| 턱(완전한 몸) | 몸 전체가 보존된 턱 있는 어류(판피류 *Xiushanosteus*, 연골어류 *Shenacanthus*). 분자시계는 턱 있는 척추동물의 기원을 늦어도 후기 오르도비스기(약 450 Ma)로 본다 | 약 436 Ma(Telychian) | - | Zhu 2022 Nature | 확인 |
| 큰 턱 어류(실루리아기) | *Megamastax amblyodus*: 포식성 해양 경골어류, 단단한 먹이를 부수는 이빨 → "데본기 이전엔 어류가 작았다(< 0.5 m)"는 가정과 그 가정을 쓴 저산소 모델을 반박 | 약 423 Ma | 약 1 m | Choo 2014 Sci Rep; 2026 재기재: Lu 2026 Nature(줄기 경골어류로 재배치) | 확인 |
| 크기 단계(어류) | 캄브리아 약 2.5–3.0 cm → 오르도비스–실루리아 수십 cm → 데본기 1 m 이상 | - | - | Dahl 2010 | 확인 |
| 캄브리아 최상위 포식자 | *Anomalocaris*: 몸길이 약 1 m. 겹눈 2–3 cm, 한 눈에 렌즈 16,000개 이상(현생 절지동물 중 가장 예리한 겹눈급) → 시각 포식자가 군비경쟁을 가속 | 약 515 Ma | 약 1 m | Paterson 2011 Nature | 확인 |

### 3.2 순서에 대한 주의

- 근육(Haootia 약 560 Ma)과 좌우대칭 이동 흔적(Ikaria, 흔적화석 560–551 Ma)은 거의 같은 시기에 나타난다. "근육·신경 → 좌우대칭" 순서는 계통학적 순서이고, 화석 연대로는 거의 동시다.
- 겹눈은 단단한 외골격보다 먼저 진화했다(Paterson 2011 요약). 눈(빛)과 포식은 캄브리아 초기에 이미 있었다.
- 척삭(Pikaia)의 화석 연대(약 505 Ma)는 최초 척추동물(약 518 Ma)보다 늦다. 화석 보존의 문제일 뿐이고, 계통상으로는 Pikaia가 더 기부(줄기) 쪽이다(Conway Morris & Caron 2012). 게임은 계통 순서를 따르면 된다.
- 턱의 기능적 다양성(하악 기계 구조)은 실루리아기에 늘었고, 데본기 첫 단계에서 안정됐다. Emsian(약 400 Ma) 산소화 사건보다 앞선다 → 턱 방산이 산소 상승 때문이라는 해석에 반대(Anderson 2011 Nature, 확인). 현생 척추동물 약 58,000종의 99% 넘게 턱이 있다.

---

## 4. 혁신의 기능적 이득과 비용

| 혁신 | 얻는 것 | 잃는 것 / 비용 | 수치 | 출처 | 확인 |
|---|---|---|---|---|---|
| 소화관(세포 밖 소화) | 가설: 섬모로 덮인 배면(mucociliary sole)이 생기며 세포 안 소화에서 세포 밖 소화로 전환 → 위강으로 들어가는 영양 농도 증가. 이어 내벽이 마디 모양 위주머니로 접히며 흡수가 최적화되고 몸이 커질 수 있었다. 몸이 커지자 협응 운동을 위해 신경망이 생겼고, 좌우대칭이 방향성 이동을 가능케 해 특화 신경계로 이어졌다 | - | 정성적 시나리오 | Arendt 2015 Phil Trans B | 확인(가설 수준) |
| 관통 소화관(입+항문) | 일방향 처리 등 | - | - | 1차 근거 미확보 | 미확인 |
| 신경계 | 행동 협응, 감각 | 뉴런 유지 비용이 크다. 휴식 상태 유지와 신호 전달 모두 비싸고, 대부분 Na+/K+ ATPase 몫. 과잉 용량은 수확 체감으로 크게 벌받는다 | - | Niven & Laughlin 2008 JEB | 확인 |
| | | 정보 1비트 비용: 화학 시냅스 10^4 ATP, 등급 신호(사이뉴런·광수용체)나 스파이크 부호화 10^6–10^7 ATP | 열역학 최소의 수 자릿수 위 | Laughlin 1998 Nat Neurosci | 확인 |
| | | 뇌 O2 소비: 척추동물 2–8%(휴식 대사 기준), 사람 20%, *Gnathonemus petersii* 약 60% | 이 물고기는 0.8 mg/L(26 °C)까지 O2 섭취·전기 방전을 유지하고, 0.3 mg/L 미만은 못 견딘다 | Nilsson 1996 JEB | 확인 |
| 머리(신경능선·플라코드) | 척추동물과 다른 척삭동물의 차이 대부분이 머리에 있다(신경능선이 결합·골격·근육 조직, 신경능선·플라코드가 감각기관). 수동 섭식(여과)에서 능동 포식으로 전환하면서 머리에 집중 | - | - | Gans & Northcutt 1983 Science | 확인 |
| 순환계 | 확산 한계를 넘어 약 1 cm 이상 복잡 생물 가능(pO2 10^3–10^4 Pa 조건) | 심장·혈관 비용(정량치 미확보) | 확산 의존은 약 1 mm | Catling 2005 | 확인 |
| | Pikaia에 배쪽 혈관 = 초기 척삭동물에 순환계 | - | - | Conway Morris & Caron 2012 | 확인 |
| 아가미 | "이온 먼저" 가설: 송어 유생에서 Na+ 흡수가 피부 → 아가미로 옮겨 가는 시기가 O2 흡수보다 빠르다 → 아가미는 처음엔 이온 조절용이었을 수 있다 | 아가미 = 이온·물이 새는 넓은 면 → 삼투 조절 비용(5장) | Na+ 이동 부화 후 15–16일, O2 이동 23–28일(50–80% 늦음) | Fu 2010 Proc R Soc B | 확인 |
| 척삭·근절(파상 유영) | 몸 대부분을 물결치는 뱀장어형 유영은 비효율적이라는 가설(Lighthill 1970, Webb 1975)과 달리 실측 효율이 꽤 높다 | 효율 측정 방법에 따라 값이 크게 다르다 | 뱀장어 효율 약 0.5(계산법에 따라 최대 0.87). 기존 다른 어류 보고값 0.74–0.97 | Tytell & Lauder 2004 JEB(원문 PDF) | 확인 |
| | 비교: 오징어의 제트 추진은 기본적으로 비효율적 → 대사율을 최대로 끌어올리는 '고에너지' 적응, 짧은 수명, 일회 번식. 어류는 장수·반복 번식에 유리 | - | 정성적 | O'Dor & Webber 1986 Can J Zool | 확인 |
| | 이동 에너지: 거리당 비용은 M^(2/3) 또는 M^(1/2)로 증가. 임계 질량 이상의 헤엄치는 동물은 무거워도 효율이 거의 같다. 나는 동물이 헤엄치는 동물보다 상대적으로 더 많이 쓴다 | - | - | Bale 2014 PNAS | 확인 |
| | 수영이 달리기·비행보다 단위 거리·체중당 비용이 낮다는 고전 비교 | - | 수치는 원문 미대조 | Schmidt-Nielsen 1972 Science | 부분(서지만 확인) |
| 눈 | 빛 감지 막 → 초점 렌즈 눈은 연속적인 작은 개선으로 가능. 비관적으로 잡아도 몇십만 년 | 감각계 에너지 비용(Niven & Laughlin 2008) | 약 1,829단계(단계마다 1% 변화), 약 363,992세대(세대당 약 0.005% 반응, 1.00005배씩) | Nilsson & Pelger 1994 Proc R Soc B | 초록 확인. 단계·세대 수는 2차 출처(Stanford Birds, Berlinski 비판문). 유전율·선택 강도는 미확인 |
| 빛 → 행동(가설) | 버제스 셰일 동물(Wiwaxia, Canadia, Marrella)에 회절격자 → 무지갯빛. 캄브리아는 포식과 상을 맺는 눈이 빠르게 진화한 시기 → 빛이 처음 행동 체계에 들어오며 진화를 흔들었다("light switch") | - | 515 Ma | Parker 1998 Proc R Soc B | 확인(가설) |
| 턱 | 섭식 기계의 기능 다양화. 현생 척추동물 99% 이상이 턱을 가짐 | 턱 기능 다양성은 실루리아기에 늘고 초기 데본기에 정체 → 이후 새 분류군은 기존 설계를 재사용 | - | Anderson 2011 Nature | 확인 |

---

## 5. 어류가 살 수 있는 수중 환경 범위

### 5.1 용존 산소

| 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|
| 해양 저서생물 206종, 실험 872건 메타분석. 관행적 저산소 정의(2 mg/L)는 시험 종 절반의 실측 아치사·치사 문턱보다 낮다 → 피해 범위를 과소평가해 왔다 | 전체 LC50 평균 2.05 ± 0.09 mg/L(중앙값 1.60), SLC50 평균 2.61 ± 0.17(중앙값 2.24). 실험의 90%가 LC50 < 4.59, SLC50 < 5.00 mg/L. LT50 평균 267.9 h(중앙값 116.7 h). 10%는 급성 저산소에서 6.8 h 미만에 절반 사망 | Vaquer-Sunyer & Duarte 2008 PNAS | 확인 |
| 분류군별 LC50 평균(90번째 백분위) | 어류 1.54(2.51), 갑각류 2.45(5.72), 연체 1.99(2.83), 환형 1.20(1.37), 극피 1.22(2.12), 자포 0.69(1.43) mg/L. 어류 SLC50 4.41(8.09) mg/L. 어류 LT50 59.9 h(10번째 백분위 0.9 h) | 같은 논문 표 1 | 확인(연체·자포 일부 칸은 표 판독이 모호) |
| 어류와 갑각류가 가장 민감하다(표에서 SLC50·LC50 상위) | - | 같은 논문 | 확인 |
| 저산소 정의와 현황 | ≤ 2 mg/L(63 µmol/L ≈ 61 µmol/kg). 1950년 이후 연안 500곳 넘게 보고. 대양은 50년 동안 O2의 약 2%(4.8 ± 2.1 Pmol, 770억 t)를 잃었다. 산소 최소층(200 m에서 < 70 µmol/kg)은 EU 크기(450만 km²)만큼 넓어졌고, 완전 무산소 수체 부피는 4배 넘게 늘었다. 생물 영향 기준으로는 '생물에 영향을 줄 만큼 낮은 산소'로 정의하는 게 더 맞다 | Breitburg 2018 Science | 확인 |
| 부영양화 → 바닥 무산소 → 폐사 메커니즘 | 비료 유출·화석연료 연소 → 1차 생산 증가 → 입자 유기물 축적 → 미생물 호흡 → 바닥물 용존 산소 소비. 데드존은 1960년대 이후 지수적으로 늘어 400곳 넘게, 24만 5천 km² 넘게 | Diaz & Rosenberg 2008 Science | 확인 |
| 양식장 가두리 어류 폐사가 저산소의 직접 피해 사례 | - | Breitburg 2018 본문 | 확인 |

### 5.2 온도

| 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|
| OCLTT(산소·용량 제한 열내성): 산소 수요와 조직 공급 능력의 불일치가 열 극한에서 전신 내성을 처음 제한한다. 장어류 *Zoarces viviparus*에서 열로 제한된 산소 운반이 성장·개체 수 감소 온도와 맞는다 | - | Pörtner & Knust 2007 Science | 확인 |
| 반대 결과: 저산소에서도 red drum, 도치(lumpfish)의 CTmax는 바뀌지 않았다(호기 범위가 72% 넘게 줄었는데도) → 상한 온도가 늘 산소 운반과 연결되지는 않는다. 새 지표 PCTmax(CTmax가 떨어지기 시작하는 수중 pO2) 제안 | > 72% 호기 범위 감소에도 CTmax 불변 | Ern, Norin, Gamperl & Esbaugh 2016 JEB | 확인 |
| OCLTT 비판 논평(다수 저자) | - | Jutfelt 2018 JEB | 부분(초록 없음. 제목·저자만) |
| 대사 지수: Φ = (O2 공급)/(휴식 대사 O2 수요). 여러 종의 분포 적도 쪽 끝이 Φ ≈ 2–5에서 끊긴다(활동에 필요한 에너지 여유). 이번 세기 온난화 + 산소 감소로 상층 해양 Φ가 전 지구 약 20%, 북쪽 고위도 약 50% 감소 예상 | Φcrit 2–5 | Deutsch 2015 Science | 확인 |
| 함수 형태: pO2_act = f(E_eco, T) / A_eco, f(E, T) = exp(−E/k_B · [1/T − 1/T_ref]). f는 E > 0이면 수온이 오를수록 커진다 → 따뜻할수록 필요한 최소 pO2가 올라간다. 휴식 내성의 E_o = E_d(대사 수요) − E_s(O2 공급). 실험실 A_o를 Φcrit = 3으로 정규화. 해양 동물 약 2만 5천 종 분석 | Φcrit 대표값 3, dE/dT 0.025 eV/°C 가정 | Penn & Deutsch 2024 Phil Trans B | 확인(본문 수식) |
| 생활사 병목: 산란 성체와 배아가 유생·비번식 성체보다 내성 범위가 좁다. 2100년 재생산 한계를 넘는 수온에 노출되는 종: SSP1-1.9 약 10%, SSP5-8.5 약 60% | 694종 | Dahlke 2020 Science | 확인 |
| 하한: 남극어 3종은 상한 치사 온도가 6 °C(어떤 생물보다 낮다). -2.5 °C 과냉각까지 생존. 중추신경계가 열 손상의 1차 부위 | 6 °C | Somero & DeVries 1967 Science | 확인 |
| 상한: 마가디 틸라피아 *Alcolapia grahami*(남서 온천 집단)는 낮 최고 43 °C, 밤 32 °C 온천에서 산다. CTmax 45.6 °C(어류 최고 기록). 대사율은 같은 크기 소형 포유류 기초 대사 수준 | 45.6 °C | Wood 2016 Sci Rep | 확인 |
| 온도-크기 규칙: 수생 동물이 더 세게 작아진다. 건중량 약 100 mg 종에서 수생 -5.0%/°C, 육상 -0.5%/°C. 작은 종(< 0.1 mg)은 비슷하고, 클수록 수생 쪽이 더 음수 → 산소 공급이 주 원인 | 1,890건, 169종 | Forster, Hirst & Atkinson 2012 PNAS | 확인 |

### 5.3 염분

| 주장 | 수치 | 출처 | 확인 |
|---|---|---|---|
| 협염성은 안정된 염분, 광염성은 변하는 염분에 산다. 광염성은 흡수 ↔ 분비, 물 배출 ↔ 보존 전략을 바꾸는 기작(삼투 감지, 신호, 상피 수송)을 가진다. 광염성은 어류에서 여러 번 독립적으로 생겼다 | 광염성 대부분 상한 약 60 g/kg(해수 2배). 일부는 130 g/kg까지(60 g/kg 넘으면 전략 전환) | Kültz 2015 JEB | 확인 |
| 서식 염분 기준으로 광염성은 소수 | < 10% | Schultz & McCormick 2013 Fish Physiol 32 | 확인(초록) |
| 같은 책 소개글 | 전체 어종의 3–5% | Fish Physiology 32 서지 소개 | 부분 |
| 실험실에서 잰 기본 염분 지위가 실제 서식 지위보다 넓다. 담수종·해수종 모두 기수를 견딘다 | - | Schultz & McCormick 2013 | 확인 |
| 삼투 조절 비용 | 옛 연구 "총 에너지 예산의 20 – 50% 이상", 최근 연구 약 10%. 중간 염분(8–20 psu)에서 성장이 더 좋은 경우가 매우 흔하지만(늘 그렇지는 않음) 표준 대사율 감소와 연결 | Boeuf & Payan 2001 Comp Biochem Physiol C | 확인 |
| | 비용은 체액과 삼투압이 같은 기수에서 가장 낮을 것으로 기대되지만, 종을 가로질러 보면 O2 섭취와 염분 사이 뚜렷한 경향이 없다. 비용 추정치는 표준 대사의 몇 %에서 30% 넘게까지 | Ern 2014 J Fish Biol | 확인 |

### 5.4 포화 용존 산소(게임의 '대기 O2·수온·염분 → 물속 O2' 변환)

APHA 표준식(Benson & Krause 1984 계열)으로 계산한 1기압 포화값(mg/L). USGS DOTABLES도 Benson & Krause 식을 쓴다(확인). 아래 숫자는 내가 식으로 계산했다(계산).

| 수온(°C) | 0 | 5 | 10 | 15 | 20 | 25 | 30 | 35 |
|---|---|---|---|---|---|---|---|---|
| 담수(염분 0) | 14.62 | 12.77 | 11.29 | 10.08 | 9.09 | 8.26 | 7.56 | 6.95 |
| 해수(염분 35) | 11.45 | 10.11 | 9.02 | 8.14 | 7.40 | 6.77 | 6.24 | 5.77 |

식: ln DO = −139.34411 + 1.575701e5/T − 6.642308e7/T² + 1.243800e10/T³ − 8.621949e11/T⁴ − S·(0.017674 − 10.754/T + 2140.7/T²), T는 켈빈, S는 염분(‰).

---

## 6. 캄브리아 폭발의 원인: 논쟁 정리

| 가설 | 핵심 | 근거 | 반대·한계 | 확인 |
|---|---|---|---|---|
| 산소 | 산소 상승이 큰 몸·활발한 대사를 허락 | Dahl 2010(550–560 Ma 산소화), Payne 2009(크기 계단과 동시) | Mills 2014(해면은 0.5–4% PAL로 충분), Sperling 2015(Fe 자료상 에디아카라–캄브리아 유의한 변화 없음), Zhang 2016(1.4 Ga에 이미 > 4% PAL) | 확인 |
| 산소 × 생태(통합) | 낮은 산소가 육식을 막았고, 산소 문턱을 넘자 포식-피식 군비경쟁이 시작 → 시기(산소)와 양상(군비경쟁)을 함께 설명 | Sperling 2013 | 현대 OMZ 대용의 한계(원생대 PAL 수치 없음) | 확인 |
| 포식(cropping) 생태 이론 | 초식자가 없으면 단세포 조류는 자원 제한으로 다양성이 낮다. '종속영양 장벽'을 넘자 초식·육식 원생생물이 거의 동시에 생기고, 영양 단계 사이 되먹임으로 수천만 년에 걸친 폭발적 다양화 | Stanley 1973 PNAS | 고전 이론(정량 검증 제한) | 확인 |
| 포식 증거 | Cloudina 천공(2.7%, 약 1/5), Anomalocaris의 예리한 눈 | Bengtson & Zhao 1992; Hua 2003; Paterson 2011 | - | 확인 |
| 빛(light switch) | 상을 맺는 눈이 생기며 빛이 행동 체계에 들어와 진화를 흔들었다 | Parker 1998 | 가설. 눈이 원인인지 결과인지 분리 어려움 | 확인(가설) |
| 인(영양염) | 스노볼 지구 직후(약 750–635 Ma) 용존 인산 농도가 매우 높았다 → 높은 생산성·유기탄소 매몰 → 산소 증가 | Planavsky 2010 Nature | - | 확인 |
| | 약 800–700 Ma까지 얕은 바다 인 매몰이 낮았다(인 제한, 레드필드 비율에서 크게 벗어난 화학량). 철 많은 무산소 바다의 인 흡착 + 영양염 기반 쌍안정성으로 저산소 세계가 안정. 800–635 Ma에 인 순환 근본 전환 → 동물 출현과 겹침 | Reinhard 2017 Nature | - | 확인 |
| 먹이 제한('불의 삼각형') | 저산소 세계는 1차 생산도 낮았을 것 → 초기 동물 군집은 생각보다 먹이가 부족. 연료(먹이)·열(온도)·산화제(산소)를 함께 봐야 한다 | Sperling & Stockey 2018 | - | 확인 |
| 생물 되먹임 | 동물·큰 진핵생물이 스스로 바다를 산소화했다(인 제거 되먹임) | Lenton 2014 | - | 확인 |
| 단일 사건이 아니다 | 571 Ma부터 여러 차례 이어진 방산. 캄브리아 폭발은 그중 한 단계(크라운 좌우대칭 동물의 방산). 산화 환원·영양 변화가 연속 방산을 유지 | Wood 2019 Nat Ecol Evol; Yang 2018(> 22 Myr) | - | 확인 |

게임 해석: 원인은 하나가 아니다. "O2 문턱 + 포식자 존재 + 영양염(P) 공급 + 눈(빛)"을 함께 쓰는 다요인 모델이 문헌에 가장 가깝다.

---

## 7. 게임 공식·문턱 설계 제안 (문헌 수치에서 유도한 것)

아래는 설계 제안이다. 문헌 사실은 위 표에 있고, 여기는 그 수치를 게임 규칙으로 옮긴 것이다.

### 7.1 혁신별 문턱 제안

| 혁신(게임) | O2 조건(대기 PAL / 칸 용존 O2) | 기타 조건 | 얻는 것 | 잃는 것 | 근거 |
|---|---|---|---|---|---|
| 다세포(군체) | 조건 없음(호기 하한 1–2% PAL만) | 포식자 밀도 또는 세균 신호 또는 낮은 영양(공공재). 가소성 단계 → 수백 세대 지속 시 유전 고정 | 포식 회피 ↑(포식자 입 크기 > 군체면 면역) | 유영 속도 ↓, 침강 ↑, 개체당 섭식 효율 약간 ↓ | Boraas, Herron, Fisher, Koschwanez, Roper, Kirkegaard |
| 세포 분화 | ≥ 0.5–4% PAL(해면급) | 군체 크기가 커질수록 필요(볼복스: 크기 ↑ → 체세포 비율 ↑) | 큰 몸 유지, 운동 | 생식세포 비율 ↓ | Mills 2014, Solari 2006, Kirk 2005 |
| 조직·소화관 | ≥ 약 5% PAL(확산 의존 약 1 mm) | - | 큰 먹이(세포 밖 소화), 몸 크기 상한 ↑ | - | Catling 2005, Arendt 2015 |
| 근육·신경 | 약 5–10% PAL | - | 협응 운동, 감각 | 휴식 대사 ↑(뇌 2–8%) | Haootia, Nilsson 1996, Niven & Laughlin |
| 좌우대칭·머리 | ≥ 약 10% PAL(캄브리아 동물 최소) | 포식(육식)은 칸 바닥 O2 ≥ 0.34 mL/L(≈0.5 mg/L)일 때만 | 방향성 이동, 능동 포식 | 신경 비용 | Dahl 2010, Payne 2009, Sperling 2013, Gans & Northcutt |
| 순환·아가미 | 10–50% PAL 구간에서 크기 상한 해제(≥ 1 cm) | 아가미는 염분 적응(이온 조절)의 전제 | 확산 한계 해제, 크기 ↑ | 아가미 면적만큼 삼투 비용 ↑ | Catling 2005, Fu 2010 |
| 척삭·근절(골격) | - | - | 파상 유영 효율(약 0.5–0.9), 장거리 이동 비용 ↓ | - | Tytell & Lauder 2004, O'Dor & Webber |
| 턱·지느러미 | 큰 포식어(≥ 1 m)는 약 40% PAL(Dahl) 또는 그보다 낮게(Choo 2014 반례 → 문턱을 경직되게 두지 말 것) | 먹잇감 크기 범위 확대 | 포식 효율 ↑ | 큰 몸 = 높은 O2 요구 | Dahl 2010, Choo 2014, Anderson 2011 |
| 눈(선택 부가) | - | 빛(수심) 있는 칸 | 포식·회피 ↑ | 감각 에너지 비용 | Nilsson & Pelger(약 36만 세대 → 게임 시간 압축 비율 결정에 사용), Parker 1998 |

### 7.2 공식 후보

1. **칸 용존 산소**: DO = DO_sat(T, S) × (대기 O2 / 1 PAL) × (1 − 바닥 소비 항). DO_sat은 5.4 표(또는 식). 부영양화(영양염 ↑) → 생산 ↑ → 침강 유기물 ↑ → 바닥 칸 O2 소비 ↑(Diaz & Rosenberg 2008).
2. **어류 생존 확률(산소)**: DO ≥ 4.6 mg/L 정상. SLC50 4.41에서 성장·번식 절반 감소. LC50 1.54에서 사망률 50%(노출 시간 축: LT50 약 60 h, 빠른 종 약 1 h). 2 mg/L는 '저산소' 경보 표시선(Vaquer-Sunyer & Duarte; Breitburg).
3. **온도 x 산소(대사 지수)**: Φ = A × pO2 × exp(E/k_B × (1/T − 1/T_ref)). 이때 E > 0이면 따뜻할수록 Φ가 작아진다(수요 증가). Φ < 1이면 휴식도 불가(사망). Φ < Φcrit(약 3)이면 활동·번식 불가. 계통마다 A, E를 유전자로 두면 진화 가능(Deutsch 2015; Penn & Deutsch 2024). 단, OCLTT 반례(Ern 2016)를 반영해 일부 계통은 산소와 무관한 고정 CTmax를 갖게 하는 것도 가능.
4. **수온 → 몸 크기**: 수생은 1 °C당 -5%까지(큰 종일수록 강함)(Forster 2012).
5. **삼투 비용**: 체액 등장점(중간 염분)에서 0에 가깝고, 담수·해수 끝으로 갈수록 표준 대사의 최대 약 10–30% 추가. 협염성 계통은 내성 폭이 좁게, 광염성 계통(전체의 < 10%)만 넓게. 상한 약 60 g/kg, 극단 계통만 130 g/kg(Kültz 2015; Ern 2014; Boeuf & Payan 2001).
6. **UV**: 표층 칸 UV 피해 ∝ 1/오존. 오존은 대기 O2의 함수(0.1% PAL 18 DU, 1% PAL 66 DU, 10% PAL 169 DU, 100% PAL 279 DU)(Cooke 2022). 수심이 깊어지면 피해가 줄어든다(수중 UV 감쇠 계수는 미확인 → 임의 상수).
7. **최대 몸 크기**: 각 혁신이 크기 상한을 연다(Payne 2009의 2단계: 진핵세포 +6자릿수, 다세포 +6자릿수). 산소는 부피가 아니라 '두께'를 제한하므로(Payne 2011), 순환계 이전에는 두께 상한(약 1 mm), 이후에는 O2 PAL에 비례한 길이 상한을 쓰는 게 문헌과 맞다.
8. **진화 속도 감각**: 군체화 10–100세대(가소성), 유전 고정 수백 세대(약 750), 눈 약 36만 세대, 볼복스 분화는 수천만 년 단위, 캄브리아 방산은 2천만 년 이상 이어짐.

---

## 8. 미확인·주의 목록

| 항목 | 상태 | 이유 / 다음 할 일 |
|---|---|---|
| Boraas 1998의 "10–20세대 뒤 8세포 군체 우세" | 부분 | Springer 원문 접근 차단. 검색 결과에 인용된 초록 문장으로만 확인 |
| Ratcliff 2012의 구체 기간(약 60일, 10개 집단 전부 등) | 미확인 | 초록은 "빠르게"만 적힘 |
| Nilsson & Pelger 1994의 유전율·변이계수·선택 강도 값 | 미확인 | 원문 PDF 403. 1,829단계·363,992세대·1.00005배/세대는 2차 출처로만 확인 |
| *Kimberella* 최대 약 15 cm | 부분 | 미러 위키 수준 출처 |
| 디킨소니아 최대 1.4 m | 부분 | 기네스 기록 페이지 |
| Pikaia 평균 약 5 cm | 부분 | ROM 보도자료(원 논문 수치 미대조) |
| *Myllokunmingia* 28 mm, *Haikouichthys* 약 2.5 cm | 부분 | 위키(Shu 1999 인용). Dahl 2010 본문의 "청장 어류 2.5–3.0 cm"는 확인 |
| 캄브리아 시작 538.8 Ma | 부분 | ICS 차트 원본 미대조(위키 경유) |
| Catling 2005 지수의 음수 부호 | 부분 | PubMed·OpenAlex 텍스트에 부호가 빠짐. 물리적으로 10^-3 m, 10^-2 m가 맞음 |
| Butterfield 2009 Geobiology(대안 시각), Butterfield 2011 PNAS 서신 | 부분 | 초록·본문 접근 불가. 제목·서지만 확인 |
| Jutfelt 2018 JEB(OCLTT 비판) | 부분 | 초록 없는 논평. 제목·저자만 |
| Schmidt-Nielsen 1972의 이동 비용 수치 | 미확인 | 초록 없음 |
| 관통 소화관(입+항문)의 이득에 대한 1차 근거 | 미확인 | 찾지 못함 |
| 순환계·심장의 에너지 비용 정량치 | 미확인 | 찾지 못함 |
| 수중 UV 감쇠 깊이 | 미확인 | 이번 범위에서 찾지 않음 |
| 광염성 비율 3–5% | 부분 | 책 소개글. 챕터 초록의 "< 10%"는 확인 |
| 포화 용존 산소 표 | 계산 | APHA 표준식으로 계산. Benson & Krause 1984 원문은 미대조 |
| Dahl 2010의 "15–50% PAL" | 확인(의역) | PMC 본문을 요약 모델로 읽음. 원문 문장 그대로는 인용 못 함 |
| Vaquer-Sunyer 표 1의 연체·자포 칸 | 확인(일부 모호) | 표 판독이 모호한 칸 있음 |

---

## 참고문헌

형식: 저자. 연도. 제목. 저널 권:쪽. DOI. [확인 방법]

1. Alegado RA, … King N. 2012. A bacterial sulfonolipid triggers multicellular development in the closest living relatives of animals. eLife 1:e00013. doi:10.7554/eLife.00013 [PubMed 초록]
2. Andreev PS, Sansom IJ, Li Q, et al. 2022. The oldest gnathostome teeth. Nature 609:964–968. doi:10.1038/s41586-022-05166-2 [PubMed 초록]
3. Anderson PSL, Friedman M, Brazeau MD, Rayfield EJ. 2011. Initial radiation of jaws demonstrated stability despite faunal and environmental change. Nature 476:206–209. doi:10.1038/nature10207 [PubMed 초록]
4. Arendt D, Benito-Gutierrez E, Brunet T, Marlow H. 2015. Gastric pouches and the mucociliary sole: setting the stage for nervous system evolution. Phil Trans R Soc B 370:20150286. doi:10.1098/rstb.2015.0286 [PubMed 초록]
5. Bale R, Hao M, Bhalla APS, Patankar NA. 2014. Energy efficiency and allometry of movement of swimming and flying animals. PNAS 111:7517–7521. doi:10.1073/pnas.1310544111 [PubMed 초록]
6. Bengtson S, Zhao Y. 1992. Predatorial borings in late Precambrian mineralized exoskeletons. Science 257:367–369. doi:10.1126/science.257.5068.367 [PubMed 초록]
7. Bobrovskiy I, Hope JM, Ivantsov A, et al. 2018. Ancient steroids establish the Ediacaran fossil Dickinsonia as one of the earliest animals. Science 361:1246–1249. doi:10.1126/science.aat7228 [PubMed 초록]
8. Bobrovskiy I, Hope JM, Nettersheim BJ, et al. 2021. Algal origin of sponge sterane biomarkers negates the oldest evidence for animals in the rock record. Nat Ecol Evol 5:165–168. doi:10.1038/s41559-020-01334-7 [Nature 초록]
9. Boeuf G, Payan P. 2001. How should salinity influence fish growth? Comp Biochem Physiol C 130:411–423. doi:10.1016/S1532-0456(01)00268-X [PubMed 초록]
10. Boraas ME, Seale DB, Boxhorn JE. 1998. Phagotrophy by a flagellate selects for colonial prey: a possible origin of multicellularity. Evol Ecol 12:153–164. doi:10.1023/A:1006527528063 [서지 Crossref, 초록은 검색 요약]
11. Breitburg D, Levin LA, Oschlies A, et al. 2018. Declining oxygen in the global ocean and coastal waters. Science 359:eaam7240. doi:10.1126/science.aam7240 [PubMed 초록 + NOAA 사본 PDF 본문]
12. Brunet T, Larson BT, Linden TA, Vermeij MJA, McDonald K, King N. 2019. Light-regulated collective contractility in a multicellular choanoflagellate. Science 366:326–334. doi:10.1126/science.aay2346 [PubMed 초록]
13. Butterfield NJ. 2009. Oxygen, animals and oceanic ventilation: an alternative view. Geobiology 7:1–7. doi:10.1111/j.1472-4669.2009.00188.x [서지만]
14. Butterfield NJ. 2011. Was the Devonian radiation of large predatory fish a consequence of rising atmospheric oxygen concentration? PNAS 108:E28. doi:10.1073/pnas.1018072108 [서지만]
15. Catling DC, Glein CR, Zahnle KJ, McKay CP. 2005. Why O2 is required by complex life on habitable planets and the concept of planetary "oxygenation time". Astrobiology 5:415–438. doi:10.1089/ast.2005.5.415 [PubMed 초록]
16. Chapelle G, Peck LS. 1999. Polar gigantism dictated by oxygen availability. Nature 399:114–115. doi:10.1038/20099 [Nature 초록]
17. Choo B, Zhu M, Zhao W, Jia L, Zhu Y. 2014. The largest Silurian vertebrate and its palaeoecological implications. Sci Rep 4:5242. doi:10.1038/srep05242 [PubMed 초록]
18. Conway Morris S, Caron J-B. 2012. Pikaia gracilens Walcott, a stem-group chordate from the Middle Cambrian of British Columbia. Biol Rev 87:480–512. doi:10.1111/j.1469-185X.2012.00220.x [OpenAlex 초록]
19. Cooke GJ, Marsh DR, Walsh C, et al. 2022. A revised lower estimate of ozone columns during Earth's oxygenated history. R Soc Open Sci 9:211165. doi:10.1098/rsos.211165 [OpenAlex 초록 + Europe PMC 본문]
20. Dahl TW, Hammarlund EU, Anbar AD, et al. 2010. Devonian rise in atmospheric oxygen correlated to the radiations of terrestrial plants and large predatory fish. PNAS 107:17911–17915. doi:10.1073/pnas.1011287107 [PubMed 초록 + PMC 본문]
21. Dahlke FT, Wohlrab S, Butzin M, Pörtner HO. 2020. Thermal bottlenecks in the life cycle define climate vulnerability of fish. Science 369:65–70. doi:10.1126/science.aaz3658 [PubMed 초록]
22. Deutsch C, Ferrel A, Seibel B, Pörtner HO, Huey RB. 2015. Climate change tightens a metabolic constraint on marine habitats. Science 348:1132–1135. doi:10.1126/science.aaa1605 [PubMed 초록]
23. Diaz RJ, Rosenberg R. 2008. Spreading dead zones and consequences for marine ecosystems. Science 321:926–929. doi:10.1126/science.1156401 [PubMed 초록]
24. dos Reis M, Thawornwattana Y, Angelis K, Telford MJ, Donoghue PCJ, Yang Z. 2015. Uncertainty in the timing of origin of animals and the limits of precision in molecular timescales. Curr Biol 25:2939–2950. doi:10.1016/j.cub.2015.09.066 [PubMed 초록]
25. Ern R, Huong DTT, Cong NV, Bayley M, Wang T. 2014. Effect of salinity on oxygen consumption in fishes: a review. J Fish Biol 84:1210–1220. doi:10.1111/jfb.12330 [Crossref 초록]
26. Ern R, Norin T, Gamperl AK, Esbaugh AJ. 2016. Oxygen dependence of upper thermal limits in fishes. J Exp Biol 219:3376–3383. doi:10.1242/jeb.143495 [PubMed 초록]
27. Evans SD, Hughes IV, Gehling JG, Droser ML. 2020. Discovery of the oldest bilaterian from the Ediacaran of South Australia. PNAS 117:7845–7850. doi:10.1073/pnas.2001045117 [PubMed 초록 + PMC 본문]
28. Fairclough SR, Dayel MJ, King N. 2010. Multicellular development in a choanoflagellate. Curr Biol 20:R875–R876. doi:10.1016/j.cub.2010.09.014 [PubMed 초록]
29. Fedonkin MA, Waggoner BM. 1997. The Late Precambrian fossil Kimberella is a mollusc-like bilaterian organism. Nature 388:868–871. doi:10.1038/42242 [Nature 초록]
30. Fisher RM, Bell T, West SA. 2016. Multicellular group formation in response to predators in the alga Chlorella vulgaris. J Evol Biol 29:551–559. doi:10.1111/jeb.12804 [OpenAlex 초록]
31. Forster J, Hirst AG, Atkinson D. 2012. Warming-induced reductions in body size are greater in aquatic than terrestrial species. PNAS 109:19310–19314. doi:10.1073/pnas.1210460109 [PubMed 초록]
32. Fu C, Wilson JM, Rombough PJ, Brauner CJ. 2010. Ions first: Na+ uptake shifts from the skin to the gills before O2 uptake in developing rainbow trout, Oncorhynchus mykiss. Proc R Soc B 277:1553–1560. doi:10.1098/rspb.2009.1545 [PubMed 초록]
33. Gans C, Northcutt RG. 1983. Neural crest and the origin of vertebrates: a new head. Science 220:268–274. doi:10.1126/science.220.4594.268 [OpenAlex 초록]
34. Gooden BA. 2014. Segmentation and oxygen diffusion in the Ediacaran Dickinsonia: an applied analysis. J Proc R Soc NSW. doi:10.5962/p.361716 [OpenAlex 초록]
35. Heim NA, Knope ML, Schaal EK, Wang SC, Payne JL. 2015. Cope's rule in the evolution of marine animals. Science 347:867–870. doi:10.1126/science.1260065 [PubMed 초록]
36. Herron MD, Hackett JD, Aylward FO, Michod RE. 2009. Triassic origin and early radiation of multicellular volvocine algae. PNAS 106:3254–3258. doi:10.1073/pnas.0811205106 [PubMed 초록]
37. Herron MD, Borin JM, Boswell JC, et al. 2019. De novo origins of multicellularity in response to predation. Sci Rep 9:2328. doi:10.1038/s41598-019-39558-8 [PubMed 초록]
38. Hua H, Pratt BR, Zhang L-Y. 2003. Borings in Cloudina shells: complex predator-prey dynamics in the terminal Neoproterozoic. Palaios 18:454–459. doi:10.1669/0883-1351(2003)018<0454:BICSCP>2.0.CO;2 [OpenAlex 초록]
39. Jutfelt F, Norin T, Ern R, et al. 2018. Oxygen- and capacity-limited thermal tolerance: blurring ecology and physiology. J Exp Biol 221:jeb169615. doi:10.1242/jeb.169615 [서지만]
40. Kirk DL. 2005. A twelve-step program for evolving multicellularity and a division of labor. BioEssays 27:299–310. doi:10.1002/bies.20197 [PubMed 초록]
41. Kirkegaard JB, Goldstein RE. 2016. Filter-feeding, near-field flows, and the morphologies of colonial choanoflagellates. Phys Rev E 94:052401. doi:10.1103/PhysRevE.94.052401 [PubMed 초록]
42. Knoll AH, Sperling EA. 2014. Oxygen and animals in Earth history. PNAS 111:3907–3908. doi:10.1073/pnas.1401745111 [서지만]
43. Koschwanez JH, Foster KR, Murray AW. 2011. Sucrose utilization in budding yeast as a model for the origin of undifferentiated multicellularity. PLoS Biol 9:e1001122. doi:10.1371/journal.pbio.1001122 [PubMed 초록]
44. Kültz D. 2015. Physiological mechanisms used by fish to cope with salinity stress. J Exp Biol 218:1907–1914. doi:10.1242/jeb.118695 [PubMed 초록]
45. Laughlin SB, de Ruyter van Steveninck RR, Anderson JC. 1998. The metabolic cost of neural information. Nat Neurosci 1:36–41. doi:10.1038/236 [Nature 초록]
46. Lenton TM, Boyle RA, Poulton SW, Shields-Zhou GA, Butterfield NJ. 2014. Co-evolution of eukaryotes and ocean oxygenation in the Neoproterozoic era. Nat Geosci 7:257–265. doi:10.1038/ngeo2108 [Nature 초록]
47. Lindsey CR, Knoll AH, Herron MD, Rosenzweig F. 2024. Fossil-calibrated molecular clock data enable reconstruction of steps leading to differentiated multicellularity and anisogamy in the Volvocine algae. BMC Biol 22:79. doi:10.1186/s12915-024-01878-1 [PubMed 초록]
48. Liu AG, Matthews JJ, Menon LR, McIlroy D, Brasier MD. 2014. Haootia quadriformis n. gen., n. sp., interpreted as a muscular cnidarian impression from the Late Ediacaran period (approx. 560 Ma). Proc R Soc B 281:20141202. doi:10.1098/rspb.2014.1202 [PubMed 초록]
49. Love GD, Grosjean E, Stalvies C, et al. 2009. Fossil steroids record the appearance of Demospongiae during the Cryogenian period. Nature 457:718–721. doi:10.1038/nature07673 [Nature 초록]
50. Lu J, Choo B, Zhao W, et al. 2026. Largest Silurian fish illuminates the origin of osteichthyan characters. Nature 651:122–127. doi:10.1038/s41586-025-10008-y [PubMed 초록]
51. Lyons TW, Reinhard CT, Planavsky NJ. 2014. The rise of oxygen in Earth's early ocean and atmosphere. Nature 506:307–315. doi:10.1038/nature13068 [Nature 초록]
52. Lyons TW, Diamond CW, Planavsky NJ, Reinhard CT, Li C. 2021. Oxygenation, life, and the planetary system during Earth's middle history: an overview. Astrobiology 21:906–923. doi:10.1089/ast.2020.2418 [PubMed 초록]
53. Martin MW, Grazhdankin DV, Bowring SA, et al. 2000. Age of Neoproterozoic bilatarian body and trace fossils, White Sea, Russia: implications for metazoan evolution. Science 288:841–845. doi:10.1126/science.288.5467.841 [PubMed 초록]
54. Mills DB, Ward LM, Jones C, Sweeten B, Forth M, Treusch AH, Canfield DE. 2014. Oxygen requirements of the earliest animals. PNAS 111:4168–4172. doi:10.1073/pnas.1400547111 [PubMed 초록]
55. Nilsson D-E, Pelger S. 1994. A pessimistic estimate of the time required for an eye to evolve. Proc R Soc B 256:53–58. doi:10.1098/rspb.1994.0048 [PubMed 초록, 세부 수치는 2차 출처]
56. Nilsson GE. 1996. Brain and body oxygen requirements of Gnathonemus petersii, a fish with an exceptionally large brain. J Exp Biol 199:603–607. doi:10.1242/jeb.199.3.603 [PubMed 초록]
57. Niven JE, Laughlin SB. 2008. Energy limitation as a selective pressure on the evolution of sensory systems. J Exp Biol 211:1792–1804. doi:10.1242/jeb.017574 [PubMed 초록]
58. O'Dor RK, Webber DM. 1986. The constraints on cephalopods: why squid aren't fish. Can J Zool 64:1591–1605. doi:10.1139/z86-241 [OpenAlex 초록]
59. Parker AR. 1998. Colour in Burgess Shale animals and the effect of light on evolution in the Cambrian. Proc R Soc B 265:967–972. doi:10.1098/rspb.1998.0385 [OpenAlex 초록]
60. Paterson JR, García-Bellido DC, Lee MSY, Brock GA, Jago JB, Edgecombe GD. 2011. Acute vision in the giant Cambrian predator Anomalocaris and the origin of compound eyes. Nature 480:237–240. doi:10.1038/nature10689 [Nature 초록]
61. Payne JL, Boyer AG, Brown JH, et al. 2009. Two-phase increase in the maximum size of life over 3.5 billion years reflects biological innovation and environmental opportunity. PNAS 106:24–27. doi:10.1073/pnas.0806314106 [PubMed 초록 + PMC 본문]
62. Payne JL, McClain CR, Boyer AG, et al. 2011. The evolutionary consequences of oxygenic photosynthesis: a body size perspective. Photosynth Res 107:37–57. doi:10.1007/s11120-010-9593-1 [PubMed 초록]
63. Penn JL, Deutsch C. 2024. Geographical and taxonomic patterns in aerobic traits of marine ectotherms. Phil Trans R Soc B 379:20220487. doi:10.1098/rstb.2022.0487 [Europe PMC 본문 수식]
64. Planavsky NJ, Rouxel OJ, Bekker A, et al. 2010. The evolution of the marine phosphate reservoir. Nature 467:1088–1090. doi:10.1038/nature09485 [Nature 초록]
65. Planavsky NJ, Reinhard CT, Wang X, et al. 2014. Low Mid-Proterozoic atmospheric oxygen levels and the delayed rise of animals. Science 346:635–638. doi:10.1126/science.1258410 [PubMed 초록]
66. Pörtner HO, Knust R. 2007. Climate change affects marine fishes through the oxygen limitation of thermal tolerance. Science 315:95–97. doi:10.1126/science.1135471 [PubMed 초록]
67. Ratcliff WC, Denison RF, Borrello M, Travisano M. 2012. Experimental evolution of multicellularity. PNAS 109:1595–1600. doi:10.1073/pnas.1115323109 [PubMed 초록]
68. Reinhard CT, Planavsky NJ, Gill BC, et al. 2017. Evolution of the global phosphorus cycle. Nature 541:386–389. doi:10.1038/nature20772 [PubMed 초록]
69. Roper M, Dayel MJ, Pepper RE, Koehl MAR. 2013. Cooperatively generated stresslet flows supply fresh fluid to multicellular choanoflagellate colonies. Phys Rev Lett 110:228104. doi:10.1103/PhysRevLett.110.228104 [PubMed 초록]
70. Schmidt-Nielsen K. 1972. Locomotion: energy cost of swimming, flying, and running. Science 177:222–228. doi:10.1126/science.177.4045.222 [서지만]
71. Schultz ET, McCormick SD. 2013. Euryhalinity in an evolutionary context. In: McCormick SD, Farrell AP, Brauner CJ (eds) Fish Physiology 32: Euryhaline Fishes. Academic Press. doi:10.1016/B978-0-12-396951-4.00010-4 [UConn 저장소 초록]
72. Shu D-G, Luo H-L, Conway Morris S, et al. 1999. Lower Cambrian vertebrates from south China. Nature 402:42–46. doi:10.1038/46965 [Nature 초록]
73. Shu D-G, Conway Morris S, Han J, et al. 2003. Head and backbone of the Early Cambrian vertebrate Haikouichthys. Nature 421:526–529. doi:10.1038/nature01264 [PubMed 초록]
74. Solari CA, Kessler JO, Michod RE. 2006. A hydrodynamics approach to the evolution of multicellularity: flagellar motility and germ-soma differentiation in volvocalean green algae. Am Nat 167:537–554. doi:10.1086/501031 [PubMed 초록]
75. Solari CA, Ganguly S, Kessler JO, Michod RE, Goldstein RE. 2006. Multicellularity and the functional interdependence of motility and molecular transport. PNAS 103:1353–1358. doi:10.1073/pnas.0503810103 [PubMed 초록]
76. Somero GN, DeVries AL. 1967. Temperature tolerance of some Antarctic fishes. Science 156:257–258. doi:10.1126/science.156.3772.257 [PubMed 초록]
77. Sperling EA, Frieder CA, Raman AV, Girguis PR, Levin LA, Knoll AH. 2013. Oxygen, ecology, and the Cambrian radiation of animals. PNAS 110:13446–13451. doi:10.1073/pnas.1312778110 [PubMed 초록 + PMC 본문]
78. Sperling EA, Wolock CJ, Morgan AS, et al. 2015. Statistical analysis of iron geochemical data suggests limited late Proterozoic oxygenation. Nature 523:451–454. doi:10.1038/nature14589 [Nature 초록]
79. Sperling EA, Stockey RG. 2018. The temporal and environmental context of early animal evolution: considering all the ingredients of an "explosion". Integr Comp Biol 58:605–622. doi:10.1093/icb/icy088 [PubMed 초록]
80. Stanley SM. 1973. An ecological theory for the sudden origin of multicellular life in the late Precambrian. PNAS 70:1486–1489. doi:10.1073/pnas.70.5.1486 [PubMed 초록]
81. Turner EC. 2021. Possible poriferan body fossils in early Neoproterozoic microbial reefs. Nature 596:87–91. doi:10.1038/s41586-021-03773-z [Nature 초록]
82. Tytell ED, Lauder GV. 2004. The hydrodynamics of eel swimming: I. Wake structure. J Exp Biol 207:1825–1841. doi:10.1242/jeb.00968 [PubMed 초록 + 원문 PDF]
83. Vaquer-Sunyer R, Duarte CM. 2008. Thresholds of hypoxia for marine biodiversity. PNAS 105:15452–15457. doi:10.1073/pnas.0803833105 [PubMed 초록 + PMC 본문]
84. Wood CM, Brix KV, De Boeck G, et al. 2016. Mammalian metabolic rates in the hottest fish on earth. Sci Rep 6:26990. doi:10.1038/srep26990 [PubMed 초록]
85. Wood R, Liu AG, Bowyer F, et al. 2019. Integrated records of environmental change and evolution challenge the Cambrian Explosion. Nat Ecol Evol 3:528–538. doi:10.1038/s41559-019-0821-6 [Nature 초록]
86. Woznica A, Cantley AM, Beemelmanns C, et al. 2016. Bacterial lipids activate, synergize, and inhibit a developmental switch in choanoflagellates. PNAS 113:7894–7899. doi:10.1073/pnas.1605015113 [PubMed 초록]
87. Yang C, Li X-H, Zhu M, Condon DJ, Chen J. 2018. Geochronological constraint on the Cambrian Chengjiang biota, South China. J Geol Soc 175:659–666. doi:10.1144/jgs2017-103 [검색 결과의 초록 인용]
88. Zhang S, Wang X, Wang H, et al. 2016. Sufficient oxygen for animal respiration 1,400 million years ago. PNAS 113:1731–1736. doi:10.1073/pnas.1523449113 [PubMed 초록]
89. Zhu Y-A, Li Q, Lu J, et al. 2022. The oldest complete jawed vertebrates from the early Silurian of China. Nature 609:954–958. doi:10.1038/s41586-022-05136-8 [PubMed 초록]
90. Benson BB, Krause D. 1984. The concentration and isotopic fractionation of oxygen dissolved in freshwater and seawater in equilibrium with the atmosphere. Limnol Oceanogr 29:620–632. [미대조. 5.4 표의 식 출처(APHA 표준식 계열). USGS DOTABLES가 이 식을 쓴다는 것만 확인]
