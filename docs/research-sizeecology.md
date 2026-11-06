# 크기 기반 생태 모델: 공식과 계수 조사

조사일 2026-11-06. 대상은 세균부터 어류까지 체질량 m 하나로 대사·섭식·포식·성장·번식·사망을 계산하는 모델이고, 게임의 계통×공간 칸(20 m × 20 m × 10 m, 0.5 m 층)에 넣을 수 있는 형태로 정리했다.

**확인 표기**
- ✅ 원문(본문·표·소스코드)을 직접 읽어 숫자를 확인함
- ◐ 초록이나 그 숫자를 인용한 믿을 만한 2차 출처로 확인함(원문 본문은 못 봄)
- ❌ 미확인. 기억이나 추정이라 쓰기 전에 원문을 봐야 함
- 🧮 위 출처의 숫자로 이 문서에서 계산한 값(출처 아님)

**원문을 직접 확인한 자료**: Hartvig et al. 2011(arXiv 판 전문, Table 1·Appendix E), Andersen et al. 2016(DTU Orbit 저자 원고. 위첨자가 텍스트로 안 뽑혀서 쪽 이미지로 숫자를 읽음), DeLong et al. 2010(PNAS 전문), mizer R 소스(CRAN 3.4.0과 GitHub 3.4.0.9003의 기본값이 같음), NUMmodel 소스(Andersen 연구실 GitHub, `input/input.h`·Fortran), Serra-Pompei et al. 2020(WUR 저자 원고, Table 1·Appendix B), Kiørboe 2013(DTU Orbit), Sumida & Moser 1980(CalCOFI), Rohan et al. 2021(NOAA 저장소 원고). 나머지는 OpenAlex·PubMed 초록으로 확인했다.

---

## 0. 단위와 변환(게임 쪽 규약 제안)

| 항목 | 값 | 출처 | 확인 |
|---|---|---|---|
| mizer 단위 | 질량 g(습중량), 길이 cm, 시간 **년**. 개체 수 단위는 면적당·부피당·전체 중 사용자가 고름 | mizer `newMultispeciesParams` 문서(Units in mizer) | ✅ |
| Hartvig 2011 단위 | g, 년, **m³**(κ = g^(λ−1)/m³), 10 °C | Hartvig et al. 2011 Table 1 | ✅ |
| NUM(단세포·요각류) 단위 | **µgC**, **일**, L, µm, Tref = 10 °C | NUMmodel `globals.f90`, `input.h` | ✅ |
| 동물플랑크톤 탄소:습중량 | 비젤라틴형 C ≈ 생체중의 5–10 %, 건중량 15–25 %. 젤라틴형 C < 0.5 %, 건중량 4–5 % | Kiørboe 2013 L&O | ✅ |
| 어류 탄소:습중량 | ≈ 10 % 가정(이 문서의 환산에 사용) | — | ❌ (일반 관행, 출처 미확인) |
| 단세포 탄소 밀도(NUM) | ρ = 0.4×10⁻⁶ µgC/µm³, r = (3m/(4πρ))^(1/3) | NUMmodel `generalists_simple.f90` | ✅ |
| 연↔일 | 1 yr = 365 d. Hartvig 계수를 일 단위로 바꾸면 h = 0.233 g^0.25/d, k = 0.0274 g^0.25/d, γ = 21.9 m³ g^−0.8/d, μ0 = 0.0023 g^0.25/d, r0 = 0.011 g^0.25/d | 🧮 | 🧮 |

권장: 내부 계산은 **g 습중량과 일**로 통일하고, NUM 계수(µgC)는 C/WW = 0.1로 바꿔 쓴다. µgC → g WW는 ×10⁻⁵이다.

---

## 1. 핵심 공식 표(게임에 바로 쓰는 것)

m = 개체 질량, m_p = 먹이 질량, W(또는 M, w_inf) = 최대(점근) 크기, m* = 성숙 크기.

| # | 항목 | 공식 | 계수(단위) | 적용 크기 | 출처 | 확인 |
|---|---|---|---|---|---|---|
| F1 | 먹이 선호 커널(로그정규) | `φ(m_p,m) = exp(−[ln(β·m_p/m)]² / (2σ²))`. mizer는 m/m_p < 1이면 0 | Hartvig β = 100, σ = 1 · **mizer 기본 β = 30, σ = 2** · NUM 단세포 β = 500, σ = 1.3 · 능동 요각류 β = 10 000, σ = 1.5 · 수동(매복) 요각류 β = 100, σ = 1 | 전 범위(그룹별 값) | Hartvig 2011 식 4·Table 1; mizer `lognormal_pred_kernel`, `setPredKernel`; NUM `input.h`; Serra-Pompei 2020 B.1.7 | ✅ |
| F2 | 먹이 가용량 | `φ_i(m) = ∫ m_p · N(m_p) · φ(m_p,m) dm_p` (질량/부피) | — | 동물 | Hartvig 2011 식 5 | ✅ |
| F3 | 탐색(청소) 부피와 조우율 | `V(m) = γ·m^q`, `E = V·φ_i` | **Hartvig: q = 0.8, γ = 0.8×10⁴ m³ g^−q yr⁻¹**(f0 = 0.6에서 역산). 유영속도×반응 단면적으로 따로 추정하면 4.3×10⁴. **mizer: q = λ − 2 + n = 0.7167**(기본값), γ는 f0 = 0.6이 되게 역산 | 어류(수 mg–kg) | Hartvig 2011 식 6, 16, Appendix E; mizer `set_q_default`, `get_gamma_default` | ✅ |
| F4 | 섭식 수준(포화)과 최대 섭식 | `f = E/(E + h·m^n)`, 섭식률 = `f·h·m^n`(Holling II) | **Hartvig: h = 85 g^(1−n) yr⁻¹, n = 0.75(10 °C)**. **mizer: n = 2/3**(`newMultispeciesParams`; 종 표 검증 기본값은 3/4). h = `(w_mat^(1−n) − w_min^(1−n)) / (age_mat·(1−n)·α·(f0 − fc))`, 성장 정보가 없으면 h = 30 | 어류 | Hartvig 2011 식 6·Table 1; mizer `h_from_species_params` | ✅ |
| F5 | 에너지 수지(성장+번식에 쓸 순에너지) | `E_g = α·f·h·m^n − k_s·m^p − k·m`. E_g < 0이면 성장 정지와 굶주림 사망(F8) | α = 0.6. **Hartvig: k = 10 g^(1−p) yr⁻¹(표준대사+활동), p = 0.75**. **mizer: p = 0.7**, `k_s = f_c·α·h·w_mat^(n−p)`, f_c = 0.2, 활동항 k = 0 | 어류 | Hartvig 2011 식 7·17; mizer `setMetabolicRate`, `get_ks_default` | ✅ |
| F6 | 임계 섭식 수준 | `f_c = k/(α·h) ≈ 0.2`. 현장 평균 f ≈ 0.4. 초기(자원만 있을 때) f0 = 0.6 | — | 어류 | Hartvig 2011 식 17–18 | ✅ |
| F7 | 성숙·번식 배분 | `ψ(m) = [1 + (m/m*)^(−u)]⁻¹ · (m/M)^(1−n)`(n = p일 때), M = m*/η. 알 생산 플럭스 `R = (ε/(2·m0)) ∫ N·ψ·E_g dm`(암컷 절반) | **η = m*/M = 0.25, u = 10, ε = 0.1, m0 = 0.5 mg(알 지름 ~1 mm)**. mizer: `w_mat = w_inf/4`, 성숙 곡선 `[1 + (w/w_mat)^(−U)]⁻¹`, U = 10, `w_mat25 = w_mat/3^(1/10)`, **w_min(알) = 0.001 g**, erepro = 1, R_max = ∞(Beverton–Holt) | 어류 | Hartvig 2011 식 9–10·Table 1; mizer `setReproduction`, `species_params` | ✅ |
| F8 | 사망률 | 포식 `μ_p(m_p) = Σ∫ φ(m_p,m)(1 − f)·V(m)·N(m) dm`(먹힌 만큼). 굶주림 `μ_s = −E_g/(ξ·m)`(E_g < 0일 때). 배경 `μ_b = μ0·m*^(n−1)` | ξ = 0.1, **μ0 = 0.84 g^(1−n) yr⁻¹**(m* = 5 kg일 때 0.1 yr⁻¹). mizer 배경 `z0 = 0.6·w_inf^(n−1)` yr⁻¹ | 어류 | Hartvig 2011 식 12–14·Appendix E; mizer `setExtMort` | ✅ |
| F9 | 경험적 자연사망(검증용) | `M = 3.00·W^−0.288 yr⁻¹`(W g, 호수·강·바다 공통) | 90 % CL: b ∈ [−0.315, −0.261], Mu ∈ [2.70, 3.30] | 치어–성어 | Lorenzen 1996 J Fish Biol | ◐ |
|  |  | `ln M = 0.55 − 1.61·ln L + 1.44·ln L∞ + ln K`(L cm, K yr⁻¹) | — | 해산 어류 | Gislason et al. 2010 Fish Fish | ◐ |
|  |  | 이론: `M(w) = c·k·w^−x`, x = 0.25 | 계수 미확인 | 어류 | Peterson & Wroblewski 1984 | ◐(형태만) |
| F10 | 자원(플랑크톤) 스펙트럼 | `dN_R/dt = r0·m^(p−1)·(κ·m^−λ − N_R) − μ_p·N_R`(세미케모스탯) | **Hartvig: κ = 5×10⁻³ g^(λ−1)/m³, λ = 2 − n + q = 2.05, r0 = 4 g^(1−p) yr⁻¹, m_cut = 0.5 g**. mizer: κ = 10¹¹(단위는 사용자 선택), λ = 2.05, resource_rate = 10, w_pp_cutoff = 10 g, 최소 1e−12 g | 자원 ≤ 0.5–10 g | Hartvig 2011 식 15·Table 1; mizer `newMultispeciesParams`; Andersen & Beyer 2006(λ = 2.05 유도) | ✅ |
| F11 | 단세포 확산 흡수(영양염·DOC) | `A_N = α_N·r⁻²/(1 + (r/r_N*)⁻²)·m` [L/d] → r ≫ r_N*이면 ∝ r ∝ **m^(1/3)** | α_N = 0.972 L d⁻¹ µgC⁻¹ µm², r_N* = 0.4 µm. 다른 판: `A_N = 3.75×10⁻⁵·m^(1/3)` L/d(m µgC) | 세균–원생생물 | NUMmodel; Serra-Pompei 2020 식 B.4; 이론은 Andersen 2016 식 3 | ✅ |
| F12 | 단세포 빛 흡수 | `A_L = (α_L/r)·(1 − e^(−r/r_L*))·m·(1 − ν)` → 작은 세포는 ∝ m, 큰 세포(r ≫ 7.5 µm)는 ∝ **m^(2/3)**. 흡수량 `J_L = ε_L·A_L·L` | α_L = 0.3 (µE m⁻² s⁻¹)⁻¹ d⁻¹ µm, r_L* = 7.5 µm, ε_L = 0.8 | 광영양 세포 | NUMmodel; 이론은 Andersen 2016 식 2(∝ l²) | ✅ |
| F13 | 단세포 포식(phagotrophy) | 청소율 `A_F = α_F·m`(**q = 1**), 최대 포식 `J_Fmax = (c_F/r)·m`(비율로 ∝ m^(−1/3)), 섭식 수준은 F4와 같은 형태 | α_F = 0.018 L d⁻¹ µgC⁻¹, c_F = 30 µm/d, ε_F = 0.8. 다른 판(Serra-Pompei): 비청소율 `0.0024·m^(−1/4)` L µgC⁻¹ d⁻¹, 최대 섭식 `0.1514·m^(−0.33)` d⁻¹ | 원생생물 | NUMmodel; Serra-Pompei 2020 식 B.6, B.10 | ✅ |
| F14 | 단세포 합성 상한·호흡·세포벽 | `J_max = α_J·m·(1 − ν)`, `J_resp = c_R·α_J·m`, `ν = 3δ/r`(세포벽 부피 비율). r ≤ 3δ = 0.15 µm이면 생존 불가 | α_J = 1.5 d⁻¹, c_R = 0.1(단순판)/0.03(비용 명시판), δ = 0.05 µm, 수동 누출 0.03/r·m. 크기 범위 m = 10⁻⁹–1 µgC | 세균–원생생물 | NUMmodel `generalists_simple.f90`, `input.h`; 최소 세포 0.15 µm은 Andersen 2016 식 8과 일치 | ✅ |
| F15 | 요각류 청소·섭식·호흡 | 청소율 `v·m^0.75` [L/d], 최대 섭식 `h·m^0.75` [µgC/d], 호흡 `κ·m^0.75`(비율로 쓰면 지수 −1/4). m µgC | Serra-Pompei 표(능동/수동): v = 0.011 / 0.0052 L µgC^−0.75 d⁻¹, h = 1.37 / 0.4, κ = 0.16 / 0.048 µgC^(1/4) d⁻¹. 동화효율 0.67, 번식효율 0.25, 성체:노플리우스 = 100. NUM(10 °C 보정): h = 0.97 / 0.29, v는 그대로, 호흡 = `0.01·ε_F·J_Fmax·Q10항 + 0.16·J_F` | 요각류 10⁻³–10³ µgC | Serra-Pompei 2020 Table 1·App. B; NUMmodel `input.h`, `copepods.f90` | ✅(Serra-Pompei 값의 기준 온도 15 °C는 추정 ❌) |
| F16 | 대사 지수의 전이 | `B = B0·M^α` | **원핵 α = 1.7(활동)/2.0(비활동), 원생생물 1.0/1.1, 후생동물 0.76/0.79**(20 °C 보정, E = 0.61 eV). 경골어 안정대사: 종 평균 b = 0.79(SE 0.11), 전체 `ln R_b = 0.80·ln M − 5.43`(R_b mmol O₂ h⁻¹, M g 습중) | 약 16자리 질량 범위 | DeLong et al. 2010; Clarke & Johnston 1999 | ✅ / ◐ |
| F17 | 최대 개체군 성장률 r_max | `r_max ∝ M^b` | **원핵 b = +0.73, 원생생물 −0.26, 후생동물 −0.23**. 생산효율 23×10⁻⁴ → 9.2×10⁻⁴ → 1.6×10⁻⁴ g J⁻¹ | 위와 같음 | DeLong et al. 2010 | ✅ |
|  |  | 조류(藻類) `μ = 3.45·C^−0.21` division/d(C pgC/세포) | 127개 관측 | 조류 | Tang 1995 | ◐ |
|  |  | 동물플랑크톤 최대 비청소율·비섭식률 ∝ V^−0.23(SE 0.12), 그룹 안 | 섬모충은 와편모충보다 2–4배, 요각류는 지각류보다 청소율 10배 | 2–2000 µm | Hansen, Bjørnsen & Hansen 1997 | ◐ |
| F18 | 온도 | `rate(T) = rate(Tref)·Q10^((T−Tref)/10)` 또는 Arrhenius | NUM: 최대율·호흡 Q10 = 2, 확산 흡수 Q10 = 1.5, Tref = 10 °C. 어류 안정대사(종간) Q10 = 1.83(0–30 °C), 종 안 순화 실험 중앙값 2.40. Hartvig 기본값은 10 °C. DeLong은 E = 0.61 eV | 전 범위 | NUMmodel `globals.f90`; Clarke & Johnston 1999; Hartvig 2011; DeLong 2010 | ✅ / ◐ |
| F19 | 시각 반응 거리 | Aksnes–Utne: `r²·exp(c·r) = |C0|·A_p·E′·E_b/(K_e + E_b)` (c 빛 감쇠 m⁻¹, A_p 먹이 보이는 면적 m², E_b 배경 광량 µE m⁻² s⁻¹, K_e 반포화, E′ = E_max/ΔS_e) | 파라미터는 종·실험마다 적합해야 함 | 시각 포식자(어류 ≳ 수 mm) | Aksnes & Utne 1997(초록); 식은 Rohan et al. 2021에서 확인 | ◐ |
|  |  | 맑은 물 한계 `R ≈ √(C0/K)·l^(2 − d/2) ≈ l^1.75`, 탁한 물에서는 물의 광학 성질이 제한 | C0 = 0.3, α = 0.001 cm⁻¹, C_t,min = 0.15, K = 0.025 cm^1.5, d = 0.5. 최대 시정 40–70 m | 어류 | Andersen 2016 Sidebar 1(Dunbrack & Ware 1987 수정판) | ✅ |
| F20 | 생태 전달 효율 | `P_TL = PP·TE^(TL−1)` | TE ≈ 10 %. 어획을 지탱하는 데 필요한 1차 생산은 전 지구 수계 1차생산의 6.2 %(외양 1.6 %, 용승·대륙붕·담수 > 20 %) | 생태계 | Pauly & Christensen 1995(FishBase 초록) | ◐ |

### 1-1. 지금 바로 쓰기 좋은 최소 세트(제안)

모든 그룹이 같은 틀을 쓰고 계수만 그룹별로 바꾼다.

```
조우      E   = A(m) * Σ_prey φ(m_p, m; β, σ) * B_prey        A = 청소율, 그룹별 (F3/F13/F15)
섭식 수준  f   = E / (E + Imax(m))                             Imax = h m^n (F4/F15), 단세포는 J_Fmax·J_max (F13/F14)
순에너지   Eg  = ε f Imax − R(m)                               R = 기초대사 + 활동 (F5/F14/F15)
성장       dm/dt = (1 − ψ) max(Eg, 0)
번식       알 수 = ε_R ψ max(Eg, 0) / (2 m0)                     단세포는 m ≥ 2 m0일 때 분열
사망       μ   = μ_pred + μ_starve(−Eg/(ξ m)) + μ_b(m)
```

---

## 2. Andersen et al. 2016의 스케일링과 특성 크기

### 2-1. 자원 획득 스케일링(길이 l, 질량 w ∝ l³)

| 메커니즘 | 청소율(clearance) | 질량 지수 | 비율(per mass) | 근거 | 확인 |
|---|---|---|---|---|---|
| 확산 섭식(영양염·DOC) | `β_N = b_N·l` | **m^(1/3)** | ∝ m^(−2/3) | 확산 경계층(Munk & Riley 1952, Fiksen 2013 리뷰). 인 친화도 자료와 맞음. 질소는 a_N = 1.2(Litchman 2007) 또는 2.25(Edwards 2012) | ✅ |
| 광합성(빛) | `β_L = b_L·l²` | **m^(2/3)** | ∝ m^(−1/3) | 자기 그늘(package effect), 단면적 | ✅ |
| 능동 포식(매복·섭식류·순항) | `β_F = b_F·l³` | **≈ m^1**(생명형을 가로지른 평균) | 크기와 무관 | 개별 섭식 방식은 l^2.8(섭식류 속도 u ≈ l^0.8 × 여과면 l²), 방식이 바뀌며 평균 ≈ 3 | ✅ |
| 계수 b_F, b_L, b_N | Table S1.2(보충자료) | — | — | 보충자료를 못 봄 → 대신 NUM 계수(F11–F13) 사용 | ❌ |

같은 크기에서 조우율이 가장 큰 전략이 우세하다. 작은 세포는 삼투영양(세균), 중간 크기는 광영양, 그 사이는 혼합영양, 큰 것은 종속영양이다. 전이 크기는 환경에 따라 움직인다. 빈영양에서는 광영양 세포가 작아지고 혼합영양 구간이 넓어진다.

### 2-2. 이동·감각

| 항목 | 값 | 확인 |
|---|---|---|
| 유영속도, 점성 영역 | u ∝ l^0.79(경험식, Kiørboe 2011) | ✅ |
| 유영속도, 관성 영역 | 이론 l^0.42(Ware 1978) 또는 l^0.5(Bejan & Marden 2006). **관측 u ∝ l^0.45** | ✅ |
| 점성↔관성 전이 | 체장 **~7 cm**(Re ~ 1000). 일반적인 전이 기준은 Re ≈ 20–30(Webb 1988). ν ≈ 10⁻² cm²/s | ✅ |
| 몸 형태 | 어류·포유류 평균 종횡비 ≈ 0.25(유선형), 요각류는 이보다 큼 | ✅ |
| 기계감각 범위 | `R ≈ (3·l_prey²·l_sensor·u_prey/u*)^(1/3)`. 난류 전단(0.03 s⁻¹)에서 상한 l ≈ 500–1000 µm → 1 cm 미만 생물에 유리 | ✅(식) / ❌(계수 c, u*) |
| 화학 기울기 추적 | 상한 ~100 µm(Batchelor 10 µm ~ Kolmogorov 1000 µm 사이). 확산 trail 기준 상한 50 µm(Kiørboe 2011) | ✅ |
| 시각 | 맑은 물에서 범위 ∝ l^1.75. 최소 망막 ~0.1 mm(막대세포 1 µm × 100² 해상도) → 수 mm 이상, 실질 하한 ~1 cm(어류·두족류 자어). 최대 시정 40–70 m(Davies-Colley & Smith 1995). 본문 요약에는 "최대 ~80 m"로 읽힘 | ✅ / 80 m는 ◐ |
| 반향정위 범위 | R ∝ l^(17/8)(방출 출력이 w^(3/4)일 때) | ✅ |

### 2-3. 특성 크기(Andersen 2016 Table 1)

| 전이 | 크기 | 결정 요인 | 확인 |
|---|---|---|---|
| 세포의 하한 | 0.15 µm ≈ 10⁻¹⁵ gC(지름 0.1–1 µm) | 세포벽(게놈은 부차적). d_limit = c_wall/c, 0.5 µm 세포에서 벽이 질량의 ~30 % | ✅ |
| 삼투영양(세균) → 광영양 | 10⁻¹⁴ – 10⁻¹³ gC | DOM 확산 섭식에서 광합성으로(식 4). C_DOM ≈ 5 µgC/L, C_L ≈ 7 J d⁻¹ m⁻²일 때 | ✅(−13은 해상도상 약간 불확실) |
| 광영양 → 혼합영양 | 10⁻⁸ gC | 영양염을 확산이 아니라 능동 섭식으로 얻음(식 5) | ✅ |
| 혼합영양 → 종속영양 | 10⁻⁷ gC(10⁻⁸–10⁻⁵ gC, l ≈ 6×10⁻² cm) | 탄소·영양염을 포식으로만(식 6). 저광·고영양염이면 10⁻⁸, 고광이면 10⁻⁵ | ✅ |
| 단세포 → 다세포 | 10⁻⁶ gC | 맥관(프랙탈 전달망) 발달(DeLong 2010 인용) | ✅(본문의 "≈ 1 µm" 표기는 이미지 판독 의심 ❌) |
| 요각류 → 어류 | ≈ 1 mg WW(~1 cm) | 기능하는 카메라 눈의 최소 크기, 점성→관성 | ✅ |
| 어류 → 고래 | ≈ 10 kg WW(본문에서는 고래 ~100 kg 이상) | 항온 대사를 유지하는 하한 | ✅ |
| 어류 크기 범위 | 1 mg – ~100 kg(1 cm – 2 m). 최대 어류는 참다랑어 450 kg, 개복치 1000 kg | | ✅ |

### 2-4. 생활사

| 항목 | 값 | 확인 |
|---|---|---|
| 섭식 `A·w^n`, n ≈ 3/4. 사망 `α·A·w^(n−1)`(α = 생리적 사망률 < 1) | 평생 번식 `R0 = (ε/(2α))·(W/w0)^(1−α)` → 성체:알 크기비가 클수록 유리 | ✅ |
| 성체:자손 질량비 | 갑각류·연골어류·고래는 고정비 **~100:1**. 경골어는 "작은 알" 전략(알 크기가 성체와 무관), 참다랑어는 성체:자어 **최대 10⁸** | ✅ |
| 요각류 성체:알 | 100–1000(산란 방식별). 모델은 100 사용 | ✅(Serra-Pompei 2020) |

---

## 3. 원생생물과 동물 사이에서 바뀌는 것(지수 전이)

| 과정 | 원핵(세균) | 원생생물(단세포 진핵) | 동물플랑크톤(요각류) | 어류 | 출처 |
|---|---|---|---|---|---|
| 대사 ∝ m^α | 1.7–2.0(초선형) | 1.0–1.1(선형) | 0.76–0.79 | 0.79–0.80(안정대사). 모델은 p = 0.75(Hartvig), 0.7(mizer) | DeLong 2010 ✅; Clarke & Johnston 1999 ◐ |
| r_max ∝ m^b | +0.73 | −0.26 | −0.23 | (≈ −1/4) | DeLong 2010 ✅ |
| 주 자원 흡수 | 확산 ∝ m^(1/3) | 빛 ∝ m^(2/3)(작은 세포는 ∝ m), 확산 ∝ m^(1/3), 포식 ∝ m | 포식만 | 포식만 | Andersen 2016 ✅; NUM ✅ |
| 청소율 지수 q | — | **1**(NUM α_F·m) | **0.75**(그룹 안. Kiørboe & Hirst 2014) | **0.8**(Hartvig), 0.7167(mizer) | ✅ |
| 청소율 절편 | — | 0.018 L d⁻¹ µgC⁻¹ | 0.011(능동)/0.0052(수동) L d⁻¹ µgC^−0.75 | γ = 0.8×10⁴ m³ g^−0.8 yr⁻¹ | ✅ |
| 최대 섭식 | 합성 상한 J_max = 1.5·m d⁻¹(선형), 포식 상한 30/r d⁻¹ | 같음 | h·m^0.75 | h·m^0.75(n = 0.75), mizer n = 2/3 | ✅ |
| 포식자:먹이 **길이**비 | — | 와편모충 1:1, 기타 편모충 3:1, 섬모충 8:1 | 윤충·요각류 18:1, 지각류·부유유생 ~50:1 | 체장비 ~5:1(질량 100에 해당) | Hansen 1994 ◐; 🧮 |
| 포식자:먹이 **질량**비 | — | 1, 27, 512(=길이비³). 모델 β = 500 | ~5800(=18³). 모델 β = 10⁴(능동), 100(수동) | β = 100(Hartvig), 30(mizer). Barnes 2010: 포식자가 클수록 PPMR이 커짐 | 🧮; Serra-Pompei ✅; Barnes 2010 ◐ |
| 먹이 폭 σ | — | 1.3 | 1.5(능동), 1(수동). 소형 생물은 σ 3–4.5(Ursin 1974) | 1(Hartvig), 2(mizer) | ✅ |
| 감각 | 화학(≤ 100 µm) | 화학·기계 | 기계(100 µm–1 cm) | 시각(≳ 1 cm) | Andersen 2016 ✅ |
| 유체 영역 | Brown 운동·점성 | 점성 | 점성(점프할 때만 관성) | 관성(~7 cm 이상) | Andersen 2016 ✅ |
| 번식 | 분열(자손 = 부모의 1/2) | 분열 | 고정비 100–1000:1 | 작은 알 0.5–1 mg, 성체:알 최대 10⁸ | Andersen 2016 ✅; Hartvig ✅ |
| 생산효율 | 23×10⁻⁴ g/J | 9.2×10⁻⁴ | 1.6×10⁻⁴ | — | DeLong 2010 ✅ |

**종합 관찰(🧮)**
- 전 생명형에 걸친 비청소율은 거의 일정하다(Kiørboe 2011: 개체군을 유지하려면 하루에 몸 부피의 **~10⁶배**를 청소해야 함 ◐). 그룹 안에서는 m^0.75로 줄고, 다음 생명형으로 넘어갈 때 절편이 계단처럼 뛴다(Kiørboe & Hirst 2014 ◐). 그래서 게임에서는 **그룹마다 q를 따로 두고 절편을 계단식으로** 두는 편이 하나의 연속 함수보다 사실에 가깝다.
- 1 g WW 생물로 환산하면(C/WW = 0.1) 요각류식 청소율은 22.6 m³/yr이고 어류(Hartvig)는 8000 m³/yr이다. **약 350배 차이**이며 시각이 생기면서 생기는 도약이다.
- 1 g WW 기준 최대 섭식 계수는 요각류(NUM, 10 °C) ≈ 20 g^0.25/yr, mizer 대체값 30, Hartvig 어류 85이다. 대략 같은 자릿수라서 연속성이 크게 깨지지 않는다.
- 섭식(최대 섭식·성장)은 생명형과 무관하게 ~3/4 지수를 따른다(Kiørboe & Hirst 2014 ◐). 반면 호흡과 청소율은 생명형마다 절편이 다르다.

---

## 4. 포식자:먹이 크기비 상세

| 대상 | 값 | 출처 | 확인 |
|---|---|---|---|
| 플랑크톤 포식자 28종 | 최적 먹이와의 **길이비**: 와편모충 1:1, 기타 편모충 3:1, 섬모충 8:1, 윤충·요각류 18:1, 지각류·부유유생 ~50:1. 여과 섭식자는 상대적으로 작은 먹이, 포착 섭식자는 큰 먹이를 선호 | Hansen, Bjørnsen & Hansen 1994 L&O | ◐ |
| 해양 먹이망 데이터(29 582건, 21곳) | PPMR이 포식자 질량에 따라 증가. 온도·위도·수심·1차생산과는 유의한 관계가 없음. PPMR이 커지면 큰 크기에서 전달효율이 줄어듦 | Barnes et al. 2010 Ecology | ◐(**기울기·평균 수치 미확인**) |
| 원자료 | 34 931건, 27곳. 포식자 0.1 mg – 415 kg, 먹이 75 pg – 4.5 kg | Barnes et al. 2008 Ecology(Ecological Archives) | ◐ |
| 어류 모델 | β = 100(Jennings 2002 인용), σ = 1(대구·가자미, Ursin 1973) | Hartvig 2011 | ✅ |
| 실현 PPMR | 커널 평균은 `exp[(λ − 3/2)σ²]·β ≈ 1.7β` | Hartvig 2011 | ✅ |
| 어류 자어 먹이 | 북태평양 대구류(Pacific hake) 자어 3.0–10.7 mm는 요각류 알·노플리우스·요각유체·성체를 먹음. 먹이 폭 50–~400 µm(대부분 70–200 µm). 먹이 크기 범위는 체장에 따라 거의 변하지 않고, 부피로는 성체 요각류가 74 %. 전갱이 첫 섭식 50–200 µm, 7 mm까지 최대 ~400 µm. 정어리·멸치·전갱이 첫 섭식 노플리우스 폭 ~70 µm(Arthur 1976 인용) | Sumida & Moser 1980 CalCOFI Rep 21 | ✅ |
|  | 상악 길이 ∝ 자어 무게^(1/3). 평균 먹이 크기는 상악 길이와 함께 커짐. 칼라누스 노플리우스·요각유체는 양의 선택, 사이클롭스는 음의 선택 | Pepin & Penney 1997 J Fish Biol | ◐ |
|  | 대구 자어·치어 10–35 mm: 상대 크기축에서 먹이 스펙트럼이 일정한지 검토, 먹이 선호를 상대 크기로 설명 | Munk 1997 J Fish Biol | ◐ |

---

## 5. 어류·동물플랑크톤 생리 수치

| 항목 | 값 | 출처 | 확인 |
|---|---|---|---|
| 어류 유영 속도 스케일 | u ∝ l^0.45(관측), 이론 최적 순항·탐색 속도 ∝ L^0.4(연어 예) → 체중당 섭식·성장 지수 0.7–0.8 | Andersen 2016 ✅; Ware 1978 ◐ |
| 최적 순항 속도 | **~1 BL/s**(이론). 온대 어류 실측 U_opt는 1–5 BL/s | Weihs 1973 Nature(2차 요약으로 확인) | ◐ |
| 임계 유영 속도 예 | 야생 bull trout 11–19 cm 3.22 BL/s, 32–42 cm 2.05 BL/s(한 종) | USGS 연구(검색 요약) | ◐ |
| 순간(burst) 속도 ~10 BL/s, 지속 시간 < 20–30 s | 혐기성. 정확한 수치 출처(Videler & Wardle 1991) 미확인 | ❌ |
| 반응 거리와 어류 크기 | 블루길 27–162 mm: 몸이 클수록 반응 거리가 비선형으로 증가하다 포화. 같은 몸 크기에서는 먹이가 클수록 멀어지고, 시각각은 거의 일정 | Breck & Gitter 1983 | ◐(**cm 수치 미확인**) |
| 반응 거리와 빛·탁도 | 0.70–215.3 lx, 1–30 JTU에서 어둡거나 탁할수록 크게 줄고, 큰 먹이일수록 더 줄어듦 | Vinyard & O'Brien 1976 | ◐ |
| 시각 모델 | 식은 F19. 빛 포화를 넣은 수정판, 파라미터는 민감도 임계값과 포화값 둘 | Aksnes & Giske 1993; Aksnes & Utne 1997; Rohan et al. 2021 | ◐ |
| 요각류 청소율 | 표 F15. 1 µgC 능동 요각류 = 0.011 L/d, 10 µgC = 0.062 L/d(🧮) | Serra-Pompei 2020 | ✅ |
| 요각류 최대 섭식 | 실험실 최대 섭식률은 온도와 무관하게 3/4 법칙. 현장 섭식은 지수가 훨씬 낮음(큰 요각류의 먹이 제한) | Saiz & Calbet 2007 L&O | ◐ |
| 요각류 사망률 | 성체 사망의 2/3–3/4이 포식. 사망은 온도와 함께 증가. 방산형은 몸이 클수록 감소, 난낭형은 크기와 무관 | Hirst & Kiørboe 2002 MEPS | ◐ |
| 어류 동화효율 | α = 0.6(SDA 15 %, 배설·분비 15 %+10 %) | Hartvig 2011 App. E | ✅ |
| 어류 최대 섭식 지수 | 0.6–0.8, 평균 n = 0.75(Jobling 1994) | Hartvig 2011 App. E | ✅ |
| 어류 표준대사 지수 | ~0.8(Winberg 1956, Killen 2007), 활동 배수 1–3 → k = 10 g^0.25/yr(활동 배수 1.5–2.1에 해당) | Hartvig 2011 App. E | ✅ |
| 지방 저장(굶주림) | ξ = 5–20 %, 기본 0.1 | Hartvig 2011 | ✅ |
| 성숙:최대 크기 | η = 0.25(Andersen 2008), mizer w_mat = w_inf/4 | ✅ |
| 알 | 0.5 mg = 지름 1 mm(Wootton 1979, Chambers 1997), mizer 1 mg | ✅ |

---

## 6. 1차 생산과 어류를 지탱하는 데 필요한 생산성

| 항목 | 값 | 출처 | 확인 |
|---|---|---|---|
| 평균 전달효율 | 10 % | Pauly & Christensen 1995 | ◐ |
| 필요 1차 생산 | 전 지구 수계 PP의 6.2 %(외양 1.6 %, 용승·대륙붕·담수 > 20 %). 다른 판 초록에는 투기 혼획 포함 8.0 % | Pauly & Christensen 1995(FishBase, WorldFish) | ◐ |
| 호수 어류 생산 | 연간 식물플랑크톤 생산과 r² = 0.79, 총인과 r² = 0.67, 현존량과 r² = 0.67(형태영양지수와는 무관). 식물플랑크톤→어류 전환 효율은 빈영양 호수가 과영양 호수보다 **~100배**. 지속 가능 어획은 생산의 ~10 % | Downing, Plante & Lalonde 1990 CJFAS | ◐(**회귀식 계수 미확인**) |
| 개체군 생산–현존량–크기 | `log10 P = 0.32 + 0.94·log10 B − 0.17·log10 W`(P kg ha⁻¹ yr⁻¹, B kg ha⁻¹, W 최대 개체 g, R² = 0.84, 38개 호수 100개 개체군) | Downing & Plante 1993 CJFAS | ◐ |
| 어류 현존량 예측 | 총인과 (저서동물 현존량/평균 수심)이 가장 좋은 예측변수(r² 0.75–0.83) | Hanson & Leggett 1982 | ◐(식 계수 미확인) |
| 해역별 먹이 사슬 길이(외양 5단계, 연안 3, 용승 1.5) | 고전 수치. 원문 미확인 | Ryther 1969 Science | ❌ |
| 어획–생산성 불일치 | 생태계 간 어획 차이는 1차생산 차이보다 훨씬 크고, 먹이망 구조·전달효율 차이로 설명 | Stock et al. 2017 PNAS | ◐ |
| 전달효율 리뷰 | TE의 범위·온도 의존성 | Eddy et al. 2021 TREE | ❌(본문 미확인) |

**🧮 계산 예시(TE = 10 %, 어류 영양단계 TL = 3–4)**

| PP (gC m⁻² yr⁻¹) | TL 3 | TL 3.5 | TL 4 |
|---|---|---|---|
| 50(빈영양 호수·외양) | 5 g WW m⁻² yr⁻¹ | 1.6 | 0.5 |
| 100(중영양) | 10 | 3.2 | 1.0 |
| 300(부영양·연안) | 30 | 9.5 | 3.0 |

P/B ≈ 0.5–1 yr⁻¹라면(❌ 가정) 현존량은 위 값의 1–2배이다. 예를 들어 중영양 TL 3.5면 3–6 g WW/m²(30–60 kg/ha)이고, 게임 기둥 하나(400 m²)에 **어류 ~1–2.5 kg**이 된다.

---

## 7. 밀도 점검(게임 칸 단위, 🧮)

Hartvig의 자원·군집 스펙트럼 `N(m) = κ·m^−λ`(κ = 5×10⁻³ g^1.05/m³, λ = 2.05)로 크기 구간 [m, 2m]의 개체 수를 계산했다. 비교용 실측값도 함께 적었다.

| m (g WW) | 대략 무엇 | 개체/m³ | 층 칸(200 m³)당 | 기둥(4000 m³)당 |
|---|---|---|---|---|
| 10⁻¹³ | 해양 세균 | 1.1×10¹¹(= 1.1×10⁵/mL) | 2×10¹³ | 4×10¹⁴ |
| 10⁻⁹ | 소형 편모충 | 6.9×10⁶ | 1.4×10⁹ | 2.8×10¹⁰ |
| 10⁻⁶ | 노플리우스·소형 요각류 | 4.9×10³(= 4.9/L) | 9.8×10⁵ | 2×10⁷ |
| 10⁻³ | 어류 자어·대형 요각류 | 3.5 | 700 | 1.4×10⁴ |
| 0.1 | 치어 | 0.028 | 5.5 | 110 |
| 1 | 소형 어류 | 0.0025 | 0.49 | 9.8 |
| 100 | 중형 어류 | 2×10⁻⁵ | 0.004 | 0.08 |
| 1000 | 대형 어류 | 1.7×10⁻⁶ | 3.5×10⁻⁴ | 0.007 |

- 실측 비교: 핀란드 Konnevesi 호(EIFAC 1980년 9월 음향 조사)에서 3.5–8 cm 물고기가 **2–25 마리/1000 m³**(= 0.002–0.025/m³), 층마다 다름(FAO 보고서 ◐). 위 표의 0.1–1 g 구간(0.003–0.03/m³)과 같은 자릿수이다.
- 세균: 이 스펙트럼으로는 ~10⁵/mL이고, 실제 바다는 ~10⁶/mL 수준이다(❌, 일반 지식). 한 자릿수 안에서 맞는다.
- 생물량: 스펙트럼 기울기가 2.05라서 크기 10배 구간마다 생물량이 거의 같다(Sheldon et al. 1972: 1 µm–10⁶ µm 사이 같은 농도 ◐). κ·ln10 ≈ **0.012 g WW/m³**(1 g 근처, 크기 10배 구간당)이다.
- 결론(🧮): 크기 10⁻³ g 이하는 칸마다 개체 수가 10²–10¹⁴로 커서 **연속량(생물량)이나 결정론**으로 다뤄야 한다. 0.1–1000 g 어류는 칸당 기대값이 0.0003–5라서 **정수 확률 계산이 꼭 필요**하다. 칸 사이 이동이 없으면 국소 멸종이 계속 일어난다(8절).

---

## 8. 확률적 개체군 계산

| 방법 | 핵심 | 출처 | 확인 |
|---|---|---|---|
| SSA(정확) | 사건 하나씩 Gillespie 알고리즘. 느림 | Gillespie 2001(서론) | ◐ |
| Poisson τ-leap | 시간 τ 동안 채널별 사건 수 ~ Poisson(a_j·τ). τ 동안 성향함수(propensity)가 "크게" 변하지 않아야 정확. 계가 커지면 화학 랑주뱅 방정식을 거쳐 결정론으로 이어짐 | Gillespie 2001 J Chem Phys | ◐ |
| τ 선택 | 성향함수가 크게 변하지 않는 범위의 최대 τ를 추정하는 개선된 절차 | Cao, Gillespie & Petzold 2006 | ◐(ε 값 미확인) |
| 음수 방지 | 이항 대체 또는 수정 Poisson(임계 반응은 SSA로 따로) | Cao, Gillespie & Petzold 2005 | ◐ |
| **이항 τ-leap** | 사건 수 ~ Binomial(N, p). 상한이 개체 수라서 음수가 안 생기고, 큰 τ를 쓸 수 있음. 한 종이 여러 채널을 쓰면 총수를 표본한 뒤 나눔 | Tian & Burrage 2004; Chatterjee, Vlachos & Katsoulakis 2005 | ◐ |
| 초개체(super-individual) | 개체 하나가 여러 실제 개체를 대표 | Scheffer et al. 1995 Ecol Model | ◐(서지만) |
| 멸종 위험 | 인구학적 확률성만 있으면 평균 멸종 시간 T가 수용력 K에 거의 **지수적으로** 증가(r̃ > 0). 환경 확률성·재난이 있으면 K의 **거듭제곱**으로만 증가. r̃ < 0이면 ln K로 증가. 큰 개체군에서는 인구학적 확률성보다 환경 확률성이 더 중요 | Lande 1993 Am Nat | ◐ |
| 멸종과 확률성 요인 | 인구학적 이질성 등 확률성을 만드는 요인에 따라 멸종 위험이 크게 달라짐(실험) | Melbourne & Hastings 2008 Nature | ◐(서지만, 본문 미확인) |

**게임 적용 제안(🧮 설계안, 문헌에서 직접 나온 것이 아님)**

칸 c, 계통 s, 크기 단계 k의 정수 N에 대해 매 Δt마다:

1. **사망(경쟁 위험)**: `μ = μ_pred + μ_starve + μ_b`. `D ~ Binomial(N, 1 − e^(−μΔt))` 뒤 다항분포 `(μ_i/μ)`로 원인을 나눈다. 포식으로 죽은 몫은 포식자 섭식량으로 넘겨 질량을 보존한다. 개체가 서로 독립이고 Δt 동안 율이 일정하면 정확하다.
2. **성장(다음 크기 단계로)**: 풍상(upwind) 이송 확률 `p_up = g(m_k)·Δt/(m_{k+1} − m_k)`, `G ~ Binomial(N − D, min(p_up, 1))`. McKendrick–von Foerster 식의 1차 풍상 차분을 정수로 바꾼 것이다. CFL 조건 `p_up ≤ 1`을 지킨다.
3. **출생**: 알 기대 수 `λ_b = Δt·Σ_k N_k·ε_R·ψ·max(E_g,0)/(2·m0)` → `Poisson(λ_b)`. 단세포는 크기 단계 2배에 도달하면 분열 = 2개로.
4. **이동**: 6방향 이웃으로 `Binomial(N, 1 − e^(−dΔt))` 뒤 다항 분배.
5. **전환 규칙**: 칸당 기대 사건 수가 ~10³을 넘으면 결정론(또는 정규 근사)으로, 그 아래는 이항으로. 미생물(< 10⁻⁶ g)은 생물량 변수, 어류는 정수가 자연스럽다(7절 표).
6. **Δt**: `max_i(μ_i·Δt) ≲ 0.05–0.1`, 그리고 `p_up ≤ 1`. 단세포는 율이 ~1 d⁻¹이라 Δt ≲ 1–2 h, 어류는 율이 ~0.01–0.1 d⁻¹이라 Δt ~ 0.5–1 d로 그룹별 하위 단계를 둘 수 있다.

---

## 9. 미확인 목록(쓰기 전에 원문 확인 필요)

1. Andersen 2016 보충자료 Table S1.2의 청소율 계수 b_F, b_L, b_N(단위 포함)과 Table S1.1 환산식. 대신 NUM 계수(✅)를 쓰면 된다.
2. Andersen 2016 기계감각 범위의 계수 c와 u* 값. 본문 "다세포 전이 10⁻⁶ gC(≈ 1 µm)"의 길이 표기(이미지 판독 의심).
3. Barnes et al. 2010의 PPMR–포식자 질량 회귀 기울기와 평균 PPMR 수치.
4. Ryther 1969의 해역별 영양단계 수(5/3/1.5)와 효율(10/15/20 %).
5. Downing et al. 1990의 어류 생산–1차생산 회귀식 계수.
6. Peterson & Wroblewski 1984 사망식의 계수(c·k).
7. 어류 유영 속도의 일반 수치(지속·순간 BL/s, Videler & Wardle 1991). 지금은 Weihs 1973의 ~1 BL/s(2차 요약)와 단일 종 자료뿐이다.
8. Breck & Gitter 1983 반응 거리의 실제 cm 값. Aksnes–Utne 파라미터(E′, K_e, C0, c)의 대표값.
9. 연안 어류 밀도(개체/m³ 또는 g/m²). 호수는 Konnevesi 음향 자료 하나만 확인했다.
10. Kiørboe & Hirst 2014의 생명형별 절편 수치(본문). Serra-Pompei가 그 자료로 적합한 값만 확인했고, Serra-Pompei 표의 기준 온도 15 °C는 NUM의 10 °C 보정 비율로 추정했다.
11. Eddy et al. 2021의 TE 범위·평균. Melbourne & Hastings 2008의 정량 결과.
12. Fenchel 1974 r_max ∝ W^−0.275는 원문 미확인이다(DeLong 2010이 "약 −0.25"로 인용한 것만 확인).
13. Kleiber 1932의 원래 지수(0.74 대 3/4). 3/4 법칙 자체는 DeLong 2010·Kiørboe & Hirst 2014·Saiz & Calbet 2007 서술로 확인했다.
14. 어류의 탄소:습중량 비(~10 %로 가정). Tang 1995 성장률 식의 온도 표준화 여부.
15. Gillooly et al. 2001의 활성화 에너지 값(DeLong이 0.61 eV로 인용한 것만 확인).

---

## 10. 참고문헌

- Aksnes, D.L. & Giske, J. 1993. A theoretical model of aquatic visual feeding. *Ecological Modelling* 67:233–250. doi:10.1016/0304-3800(93)90007-F
- Aksnes, D.L. & Utne, A.C.W. 1997. A revised model of visual range in fish. *Sarsia* 82:137–147. doi:10.1080/00364827.1997.10413647
- Andersen, K.H. & Beyer, J.E. 2006. Asymptotic size determines species abundance in the marine size spectrum. *American Naturalist* 168:54–61. doi:10.1086/504849
- Andersen, K.H., Beyer, J.E., Pedersen, M., Andersen, N.G. & Gislason, H. 2008. Life-history constraints on the success of the many small eggs reproductive strategy. *Theoretical Population Biology* 73:490–497. doi:10.1016/j.tpb.2008.02.001
- Andersen, K.H., Aksnes, D.L., Berge, T., Fiksen, Ø. & Visser, A. 2015. Modelling emergent trophic strategies in plankton. *Journal of Plankton Research* 37:862–868. doi:10.1093/plankt/fbv054
- Andersen, K.H., Berge, T., Gonçalves, R.J., Hartvig, M., Heuschele, J., Hylander, S., Jacobsen, N.S., Lindemann, C., Martens, E.A., Neuheimer, A.B., Olsson, K., Palacz, A., Prowe, A.E.F., Sainmont, J., Traving, S.J., Visser, A.W., Wadhwa, N. & Kiørboe, T. 2016. Characteristic sizes of life in the oceans, from bacteria to whales. *Annual Review of Marine Science* 8:217–241. doi:10.1146/annurev-marine-122414-034144 (PMID 26163011; 저자 원고 DTU Orbit)
- Andersen, K.H. & Visser, A.W. 2023. From cell size and first principles to structure and function of unicellular plankton communities. *Progress in Oceanography* 213:102995. doi:10.1016/j.pocean.2023.102995 (초록만 확인. 계수는 같은 연구실의 NUMmodel 코드에서 가져옴)
- Banse, K. 1982. Cell volumes, maximal growth rates of unicellular algae and ciliates, and the role of ciliates in the marine pelagial. *Limnology and Oceanography* 27:1059–1071. doi:10.4319/lo.1982.27.6.1059
- Barnes, C., Bethea, D.M., Brodeur, R.D., Spitz, J. et al. 2008. Predator and prey body sizes in marine food webs. *Ecology* 89:881. doi:10.1890/07-1551.1
- Barnes, C., Maxwell, D., Reuman, D.C. & Jennings, S. 2010. Global patterns in predator–prey size relationships reveal size dependency of trophic transfer efficiency. *Ecology* 91:222–232. doi:10.1890/08-2061.1
- Blanchard, J.L., Heneghan, R.F., Everett, J.D., Trebilco, R. & Richardson, A.J. 2017. From bacteria to whales: using functional size spectra to model marine ecosystems. *Trends in Ecology & Evolution* 32:174–186. doi:10.1016/j.tree.2016.12.003
- Breck, J.E. & Gitter, M.J. 1983. Effect of fish size on the reactive distance of bluegill (*Lepomis macrochirus*) sunfish. *Canadian Journal of Fisheries and Aquatic Sciences* 40:162–167. doi:10.1139/f83-026
- Brown, J.H., Gillooly, J.F., Allen, A.P., Savage, V.M. & West, G.B. 2004. Toward a metabolic theory of ecology. *Ecology* 85:1771–1789. doi:10.1890/03-9000
- Cao, Y., Gillespie, D.T. & Petzold, L.R. 2005. Avoiding negative populations in explicit Poisson tau-leaping. *Journal of Chemical Physics* 123:054104. doi:10.1063/1.1992473
- Cao, Y., Gillespie, D.T. & Petzold, L.R. 2006. Efficient step size selection for the tau-leaping simulation method. *Journal of Chemical Physics* 124:044109. doi:10.1063/1.2159468
- Chatterjee, A., Vlachos, D.G. & Katsoulakis, M.A. 2005. Binomial distribution based τ-leap accelerated stochastic simulation. *Journal of Chemical Physics* 122:024112. doi:10.1063/1.1833357
- Clarke, A. & Johnston, N.M. 1999. Scaling of metabolic rate with body mass and temperature in teleost fish. *Journal of Animal Ecology* 68:893–905. doi:10.1046/j.1365-2656.1999.00337.x
- DeLong, J.P., Okie, J.G., Moses, M.E., Sibly, R.M. & Brown, J.H. 2010. Shifts in metabolic scaling, production, and efficiency across major evolutionary transitions of life. *PNAS* 107:12941–12945. doi:10.1073/pnas.1007783107 (PMC2919978)
- Downing, J.A., Plante, C. & Lalonde, S. 1990. Fish production correlated with primary productivity, not the morphoedaphic index. *Canadian Journal of Fisheries and Aquatic Sciences* 47:1929–1936. doi:10.1139/f90-217
- Downing, J.A. & Plante, C. 1993. Production of fish populations in lakes. *Canadian Journal of Fisheries and Aquatic Sciences* 50:110–120. doi:10.1139/f93-013
- Eddy, T.D., Bernhardt, J.R., Blanchard, J.L., Cheung, W.W.L. et al. 2021. Energy flow through marine ecosystems: confronting transfer efficiency. *Trends in Ecology & Evolution* 36:76–86. doi:10.1016/j.tree.2020.09.006
- EIFAC. EIFAC experiments on pelagic fish stock assessment by acoustic methods in Lake Konnevesi, Finland(조사 1980년 9월), Section 7. FAO 문서 AC870E. https://www.fao.org/4/ac870e/AC870E07.htm (저자·연도 미표기)
- Fenchel, T. 1974. Intrinsic rate of natural increase: the relationship with body size. *Oecologia* 14:317–326. doi:10.1007/BF00384576
- Gillespie, D.T. 2001. Approximate accelerated stochastic simulation of chemically reacting systems. *Journal of Chemical Physics* 115:1716–1733. doi:10.1063/1.1378322
- Gillooly, J.F., Brown, J.H., West, G.B., Savage, V.M. & Charnov, E.L. 2001. Effects of size and temperature on metabolic rate. *Science* 293:2248–2251. doi:10.1126/science.1061967
- Gislason, H., Daan, N., Rice, J.C. & Pope, J.G. 2010. Size, growth, temperature and the natural mortality of marine fish. *Fish and Fisheries* 11:149–158. doi:10.1111/j.1467-2979.2009.00350.x
- Hansen, B.W., Bjørnsen, P.K. & Hansen, P.J. 1994. The size ratio between planktonic predators and their prey. *Limnology and Oceanography* 39:395–403. doi:10.4319/lo.1994.39.2.0395
- Hansen, P.J., Bjørnsen, P.K. & Hansen, B.W. 1997. Zooplankton grazing and growth: scaling within the 2–2,000-µm body size range. *Limnology and Oceanography* 42:687–704. doi:10.4319/lo.1997.42.4.0687
- Hanson, J.M. & Leggett, W.C. 1982. Empirical prediction of fish biomass and yield. *Canadian Journal of Fisheries and Aquatic Sciences* 39:257–263. doi:10.1139/f82-036
- Hartvig, M., Andersen, K.H. & Beyer, J.E. 2011. Food web framework for size-structured populations. *Journal of Theoretical Biology* 272:113–122. doi:10.1016/j.jtbi.2010.12.006 (arXiv:1004.4138)
- Hirst, A.G. & Kiørboe, T. 2002. Mortality of marine planktonic copepods: global rates and patterns. *Marine Ecology Progress Series* 230:195–209. doi:10.3354/meps230195
- Kiørboe, T. 2011. How zooplankton feed: mechanisms, traits and trade-offs. *Biological Reviews* 86:311–339. doi:10.1111/j.1469-185X.2010.00148.x
- Kiørboe, T. 2013. Zooplankton body composition. *Limnology and Oceanography* 58:1843–1850. doi:10.4319/lo.2013.58.5.1843
- Kiørboe, T. & Hirst, A.G. 2014. Shifts in mass scaling of respiration, feeding, and growth rates across life-form transitions in marine pelagic organisms. *American Naturalist* 183:E118–E130. doi:10.1086/675241
- Kleiber, M. 1932. Body size and metabolism. *Hilgardia* 6:315–353. doi:10.3733/hilg.v06n11p315
- Lande, R. 1993. Risks of population extinction from demographic and environmental stochasticity and random catastrophes. *American Naturalist* 142:911–927. doi:10.1086/285580
- Lorenzen, K. 1996. The relationship between body weight and natural mortality in juvenile and adult fish: a comparison of natural ecosystems and aquaculture. *Journal of Fish Biology* 49:627–642. doi:10.1111/j.1095-8649.1996.tb00060.x
- Melbourne, B.A. & Hastings, A. 2008. Extinction risk depends strongly on factors contributing to stochasticity. *Nature* 454:100–103. doi:10.1038/nature06922
- mizer R 패키지: Scott, F., Blanchard, J.L. & Andersen, K.H. 2014. mizer: an R package for multispecies, trait-based and community size spectrum ecological modelling. *Methods in Ecology and Evolution* 5:1121–1125. doi:10.1111/2041-210X.12256. 기본값은 CRAN 3.4.0과 GitHub sizespectrum/mizer 3.4.0.9003 소스(`newMultispeciesParams.R`, `species_params.R`, `setPredKernel.R`, `setMetabolicRate.R`, `setReproduction.R`, `setExtMort.R`, `setSearchVolume.R`)에서 확인(2026-11-06)
- Munk, P. 1997. Prey size spectra and prey availability of larval and small juvenile cod. *Journal of Fish Biology* 51:340–351. doi:10.1111/j.1095-8649.1997.tb06107.x
- NUMmodel(Andersen 연구실): github.com/Kenhasteandersen/NUMmodel, `input/input.h`, `Fortran/generalists_simple.f90`, `copepods.f90`, `globals.f90`(2026-11-06 main 브랜치)
- Pauly, D. & Christensen, V. 1995. Primary production required to sustain global fisheries. *Nature* 374:255–257. doi:10.1038/374255a0
- Pepin, P. & Penney, R.W. 1997. Patterns of prey size and taxonomic composition in larval fish: are there general size-dependent models? *Journal of Fish Biology* 51(Suppl. A):84–100. doi:10.1111/j.1095-8649.1997.tb06094.x
- Peterson, I. & Wroblewski, J.S. 1984. Mortality rate of fishes in the pelagic ecosystem. *Canadian Journal of Fisheries and Aquatic Sciences* 41:1117–1120. doi:10.1139/f84-131
- Rohan, S.K., Beauchamp, D.A., Essington, T.E. & Hansen, A.G. 2021. Merging empirical and mechanistic approaches to modeling aquatic visual foraging using a generalizable visual reaction distance model. *Ecological Modelling*: 109688. doi:10.1016/j.ecolmodel.2021.109688 (NOAA IR noaa:32042)
- Ryther, J.H. 1969. Photosynthesis and fish production in the sea. *Science* 166:72–76. doi:10.1126/science.166.3901.72
- Saiz, E. & Calbet, A. 2007. Scaling of feeding in marine calanoid copepods. *Limnology and Oceanography* 52:668–675. doi:10.4319/lo.2007.52.2.0668
- Scheffer, M., Baveco, J.M., DeAngelis, D.L., Rose, K.A. & van Nes, E.H. 1995. Super-individuals a simple solution for modelling large populations on an individual basis. *Ecological Modelling* 80:161–170. doi:10.1016/0304-3800(94)00055-M
- Serra-Pompei, C., Soudijn, F., Visser, A.W., Kiørboe, T. & Andersen, K.H. 2020. A general size- and trait-based model of plankton communities. *Progress in Oceanography* 189:102473. doi:10.1016/j.pocean.2020.102473 (저자 원고 edepot.wur.nl/536634)
- Sheldon, R.W., Prakash, A. & Sutcliffe, W.H. 1972. The size distribution of particles in the ocean. *Limnology and Oceanography* 17:327–340. doi:10.4319/lo.1972.17.3.0327
- Stock, C.A., John, J.G., Rykaczewski, R.R., Asch, R.G. et al. 2017. Reconciling fisheries catch and ocean productivity. *PNAS* 114:E1441–E1449. doi:10.1073/pnas.1610238114
- Sumida, B.Y. & Moser, H.G. 1980. Food and feeding of Pacific hake larvae, *Merluccius productus*, off southern California and northern Baja California. *CalCOFI Reports* 21. (쪽수 미확인)
- Tang, E.P.Y. 1995. The allometry of algal growth rates. *Journal of Plankton Research* 17:1325–1335. doi:10.1093/plankt/17.6.1325
- Tian, T. & Burrage, K. 2004. Binomial leap methods for simulating stochastic chemical kinetics. *Journal of Chemical Physics* 121:10356–10364. doi:10.1063/1.1810475
- Vinyard, G.L. & O'Brien, W.J. 1976. Effects of light and turbidity on the reactive distance of bluegill (*Lepomis macrochirus*). *Journal of the Fisheries Research Board of Canada* 33:2845–2849. doi:10.1139/f76-342
- Ware, D.M. 1978. Bioenergetics of pelagic fish: theoretical change in swimming speed and ration with body size. *Journal of the Fisheries Research Board of Canada* 35:220–228. doi:10.1139/f78-036
- Weihs, D. 1973. Optimal fish cruising speed. *Nature* 245:48–50. doi:10.1038/245048a0
