export const meta = {
  name: 'kebbi-topics-tablet-proof',
  description: '為5組學生發想Kebbi競賽教案與生活應用主題,以對抗式平板測試確保平板做不到',
  phases: [
    { title: 'Generate', detail: '每組×三大類(智慧教學/互動遊戲/生活應用)各生成5個候選,含多機協作' },
    { title: 'TabletTest', detail: '兩位平板辯護律師對抗式驗證,雙雙攻不下才存活' },
    { title: 'Assign', detail: '跨組去重,每組選定3個最佳主題' },
    { title: 'Develop', detail: '完整展開15個選定主題' },
    { title: 'Critique', detail: '評審視角體檢:平板免疫/跨組差異/發展適配/評分對齊' },
  ],
}

// ---------- 共用：差異化鐵則 ----------
const DOCTRINE = [
  '【鐵則・平板測試】平板/手機本身已具備:觸控螢幕、前後攝影機、麥克風、喇叭、網路、TTS、ASR,且可架在腳架上。',
  '因此「辨識物件→語音回饋」「出題問答」「對話聊天」「朗讀/翻譯」「測驗計分」這類功能,平板App都做得到,一律不算Kebbi的特色。',
  'Kebbi 真正勝過平板、平板做不到的能力有五大類(白名單),作品必須以其中至少一項為「核心機制」(拿掉它作品就垮):',
  '  1) 移動(前進/後退/旋轉):在實體空間走位、主動靠近人、帶路導覽、巡邏、佈置地面闖關、空間中追/躲。',
  '  2) 關節手勢(頭/雙手):比手勢、肢體示範動作、跳舞律動、比手語、用手指向實體物、點頭搖頭的身體語言。',
  '  3) 聲源定位(方向性麥克風)+轉向:判斷誰在說、在哪個方位並轉頭/轉身面向說話者,支援多人空間互動、點名、主持。',
  '  4) 實體在場:上述綜合而成、一個會移動會轉頭會比手勢的實體夥伴帶來的社會臨場感與物理互動,螢幕無法取代。',
  '  5) 多機協作(2 台以上 Kebbi):多台機器人在同一實體空間分工、對話接力、多角色演出、合作或對戰遊戲、交棒移動。多個會動會比手勢的實體智能體在空間中協作,連多台平板都無法取代其物理臨場與移動。這是最強的平板免疫,鼓勵高年級組與互動遊戲類採用。',
  '凡是拿掉移動/關節/聲源定位/多機協作後,用平板就能達到同樣學習或生活成效的點子,一律不合格。',
].join('\n')

const HW_ENUM = ['移動', '關節手勢', '聲源定位', '實體在場', '多機協作']
const CATS = ['智慧教學', '互動遊戲', '生活應用']

const GROUPS = [
  { id: 'G1', level: '小學五年級升六年級', age: '10-11歲', students: '1人(獨立一組)',
    traits: '具象思維、愛遊戲與肢體活動、注意力短、需要陪伴感與即時成就感;一人一組宜採與機器人一對一互動、避免需多人才成立的設計', prog: '積木式視覺程式(Blockly)為主' },
  { id: 'G2', level: '國二升國三', age: '13-14歲', students: '一組(屬13人四組之一)',
    traits: '抽象思維萌芽、面臨會考壓力、開始做實驗、重視同儕互動、可寫進階積木或入門Python', prog: '進階積木/入門Python' },
  { id: 'G3', level: '國三升高一', age: '14-15歲', students: '一組(屬13人四組之一)',
    traits: '銜接轉換期、生涯探索、自主學習、認同感重要、人文關懷題材契合', prog: 'Python入門/串接API的概念' },
  { id: 'G4', level: '高一升高二', age: '15-16歲', students: '一組(屬13人四組之一)',
    traits: '學科加深、能寫較完整程式並串接雲端AI/外部API、適合有資料與系統感的作品', prog: 'Python/串接雲端API與感測整合' },
  { id: 'G5', level: '高三升大學', age: '17-18歲', students: '一組(屬13人四組之一)',
    traits: '接近大專水準、能做完整系統整合與多模態、處理真實場域問題、衝大獎', prog: '完整系統開發/多模態與機器人控制整合' },
]

// ---------- Schemas ----------
const CANDIDATES_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    candidates: {
      type: 'array', minItems: 5, maxItems: 5,
      items: {
        type: 'object', additionalProperties: false,
        properties: {
          title: { type: 'string' },
          user_story: { type: 'string' },
          kebbi_unique_hw: { type: 'array', minItems: 1, items: { type: 'string', enum: HW_ENUM } },
          kebbi_count: { type: 'string', description: '需要幾台 Kebbi,例如 1台 / 2台 / 3台以上' },
          feature_combo: { type: 'string' },
          flow: { type: 'string' },
          why_not_tablet: { type: 'string' },
        },
        required: ['title', 'user_story', 'kebbi_unique_hw', 'kebbi_count', 'feature_combo', 'flow', 'why_not_tablet'],
      },
    },
  },
  required: ['candidates'],
}

const VERDICT_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    tablet_can_replicate: { type: 'boolean' },
    tablet_counter_design: { type: 'string' },
    genuinely_lost_without_kebbi: { type: 'string' },
    confidence: { type: 'string', enum: ['low', 'medium', 'high'] },
  },
  required: ['tablet_can_replicate', 'tablet_counter_design', 'genuinely_lost_without_kebbi', 'confidence'],
}

const ASSIGN_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    assignments: {
      type: 'array',
      items: {
        type: 'object', additionalProperties: false,
        properties: {
          group_id: { type: 'string' },
          category: { type: 'string' },
          chosen_title: { type: 'string' },
          rationale: { type: 'string' },
          needs_strengthening: { type: 'boolean' },
        },
        required: ['group_id', 'category', 'chosen_title', 'rationale', 'needs_strengthening'],
      },
    },
    diversity_summary: { type: 'string' },
  },
  required: ['assignments', 'diversity_summary'],
}

const DEVELOP_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    title: { type: 'string' },
    markdown: { type: 'string' },
    kebbi_unique_hw: { type: 'string' },
    tablet_proof_one_liner: { type: 'string' },
    challenge: { type: 'string' },
  },
  required: ['title', 'markdown', 'kebbi_unique_hw', 'tablet_proof_one_liner', 'challenge'],
}

const CRITIC_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    overall: { type: 'string' },
    diversity_check: { type: 'string' },
    weakest_topics: { type: 'array', items: { type: 'string' } },
    improvements: { type: 'array', items: { type: 'string' } },
  },
  required: ['overall', 'diversity_check', 'weakest_topics', 'improvements'],
}

// ---------- Prompt builders ----------
function genPrompt(group, category) {
  let catNote = '本類為日常生活情境,需打中具體生活痛點與使用者。'
  if (category === '智慧教學') catNote = '本類為智慧教學情境(融入學科、正式或非正式教育),需能對應課綱素養與學習目標,Kebbi 扮演會動會示範的教學夥伴。'
  if (category === '互動遊戲') catNote = '本類為互動/體感遊戲(學習遊戲化、闖關、競賽、體感律動、多人或多機對戰合作),強調趣味、即時回饋與身體參與。'
  return [
    '你是 Kebbi 凱比機器人競賽的資深選題教練。請為以下這一組學生,在「' + category + '」這一大類,發想剛好 5 個強而有力、彼此不同的候選主題。',
    '',
    '【學生組別】',
    '- 程度:' + group.level + '(' + group.age + ')',
    '- 人數/型態:' + group.students,
    '- 發展特性:' + group.traits,
    '- 可用程式深度:' + group.prog,
    '',
    '【競賽脈絡】STEM人文跨域與AI應用競賽。評分重應用性(真實需求)、創意性、挑戰性;決賽看 Kebbi 整體成果與現場實體展演(會動、會比手勢最吃香)。',
    catNote,
    '',
    DOCTRINE,
    '',
    '【任務要求】',
    '1. 每個候選主題都必須以白名單(移動/關節手勢/聲源定位/實體在場/多機協作)中至少一項為核心機制,且該機制是作品成立的關鍵。',
    '2. 不要提出攝影機辨識+語音回饋、純問答/朗讀/翻譯/測驗這類平板就能做的點子。',
    '3. 5 個候選盡量涵蓋不同領域與情境,避免互相雷同。',
    '4. 發展程度必須適配這組學生(難度、互動方式、程式深度)。',
    '5. 鼓勵(尤其高年級組與互動遊戲類)發展需要 2 台以上 Kebbi 協作才成立的應用(多機分工、對話接力、多角色戲劇、機器人對戰或合作);此類本質上平板絕對做不到。請在 kebbi_count 註明需要幾台。',
    '請輸出 5 個候選,每個含:title、user_story(誰/在什麼情境/想要什麼/痛點,一句話)、kebbi_unique_hw(白名單項目)、kebbi_count(需要幾台 Kebbi)、feature_combo(功能組合)、flow(運作流程)、why_not_tablet(為何平板做不到,需點出物理/空間/肢體/多機上的依賴)。',
  ].join('\n')
}

function skepticBase(c, group, category) {
  return [
    '【被檢驗的 Kebbi 作品】',
    '- 組別:' + group.level + '｜類別:' + category,
    '- 主題:' + c.title,
    '- 使用者故事:' + c.user_story,
    '- 宣稱用到的 Kebbi 獨有能力:' + (c.kebbi_unique_hw || []).join('、'),
    '- 需要 Kebbi 台數:' + (c.kebbi_count || '未註明'),
    '- 功能組合:' + c.feature_combo,
    '- 運作流程:' + c.flow,
    '- 作者主張為何平板做不到:' + c.why_not_tablet,
  ].join('\n')
}

function skepticTablet(c, group, category) {
  return [
    '你是「平板辯護律師」,任務是用最強的平板/手機 App 方案,證明以下 Kebbi 作品其實不需要機器人。',
    '',
    skepticBase(c, group, category),
    '',
    '【關鍵事實】平板/手機本身就有:觸控螢幕、前後攝影機、麥克風、喇叭、網路、TTS、ASR,且可架在腳架上、可外接喇叭。',
    '【你的工作】設計一個盡可能完整的平板/手機 App,試圖達成同樣的學習/生活成效。然後嚴格判定:',
    '- tablet_can_replicate:平板能否達成實質相同的成效?預設傾向 true。只有當成效真的依賴在實體空間中移動/用關節肢體示範或比手語/轉頭定位說話者這些平板沒有的物理能力,且少了它成效會明顯改變時,才填 false。若作品需要多台 Kebbi 在同一空間移動與比手勢協作,請誠實評估:多台只能定點、無手臂、不能移動的平板能否達成同樣的物理協作?通常不能,此時應填 false。',
    '- tablet_counter_design:你的平板方案具體怎麼做。',
    '- genuinely_lost_without_kebbi:若真有,少了 Kebbi 的身體會具體失去什麼;若無,寫無實質損失。',
    '- confidence:你判斷的信心。',
    '務必嚴格、挑剔,不要被作者的說法說服。',
  ].join('\n')
}

function skepticAlt(c, group, category) {
  return [
    '你是「現成替代方案律師」,主張用智慧音箱(Google Nest/Amazon Echo,具語音互動與基本聲音處理)+ 手機視訊 + 一般平板 App 的組合就能取代以下 Kebbi 作品,不必特地用機器人。',
    '',
    skepticBase(c, group, category),
    '',
    '【你的工作】用上述現成裝置組合設計替代方案,並嚴格判定:',
    '- tablet_can_replicate:現成裝置能否達成實質相同成效?預設傾向 true,除非作品成效真的依賴 Kebbi 會在空間中移動、用關節做肢體示範/手語、轉身面向說話者的物理臨場。',
    '- tablet_counter_design:你的替代方案具體怎麼做。',
    '- genuinely_lost_without_kebbi:少了 Kebbi 的身體具體失去什麼(沒有就寫無實質損失)。',
    '- confidence。',
    '務必嚴格、挑剔。',
  ].join('\n')
}

// ---------- Phase 1+2: 生成 → 對抗式平板測試 (pipeline) ----------
phase('Generate')
const units = []
for (const g of GROUPS) {
  for (const cat of CATS) units.push({ group: g, category: cat })
}

const tested = await pipeline(
  units,
  (unit) => agent(genPrompt(unit.group, unit.category), {
    label: '生成:' + unit.group.id + '-' + unit.category, phase: 'Generate',
    schema: CANDIDATES_SCHEMA, effort: 'medium',
  }),
  (gen, unit) => {
    const cs = gen && gen.candidates ? gen.candidates : []
    const thunks = cs.map((c, ci) => () =>
      parallel([
        () => agent(skepticTablet(c, unit.group, unit.category), {
          label: '平板律師:' + unit.group.id + '-' + unit.category + '#' + (ci + 1), phase: 'TabletTest',
          schema: VERDICT_SCHEMA, effort: 'medium',
        }),
        () => agent(skepticAlt(c, unit.group, unit.category), {
          label: '替代律師:' + unit.group.id + '-' + unit.category + '#' + (ci + 1), phase: 'TabletTest',
          schema: VERDICT_SCHEMA, effort: 'medium',
        }),
      ]).then((verdicts) => {
        const vs = verdicts.filter(Boolean)
        const survives = vs.length === 2 && vs.every((v) => v.tablet_can_replicate === false)
        return Object.assign({}, c, { group_id: unit.group.id, category: unit.category, verdicts: vs, survives: survives })
      })
    )
    return parallel(thunks).then((results) => ({
      group_id: unit.group.id, category: unit.category, candidates: results.filter(Boolean),
    }))
  }
)

// ---------- 整理結果 ----------
const allCandidates = tested.filter(Boolean).flatMap((t) => t.candidates)
const byKey = {}
for (const g of GROUPS) {
  for (const cat of CATS) byKey[g.id + '|' + cat] = { survivors: [], failed: [] }
}
for (const c of allCandidates) {
  const slot = byKey[c.group_id + '|' + c.category]
  if (!slot) continue
  if (c.survives) slot.survivors.push(c)
  else slot.failed.push(c)
}
const candByTitle = {}
for (const c of allCandidates) candByTitle[c.group_id + '|' + c.category + '|' + c.title] = c

const totalCand = allCandidates.length
const totalSurv = allCandidates.filter((c) => c.survives).length
log('生成候選 ' + totalCand + ' 個,通過雙重平板測試存活 ' + totalSurv + ' 個')

// ---------- Phase 3: 跨組去重指派 ----------
phase('Assign')
function listFor(slot) {
  const pool = slot.survivors.length ? slot.survivors : slot.failed
  const tag = slot.survivors.length ? '[存活]' : '[無存活,待強化]'
  return pool.map((c) =>
    '  ' + tag + ' 「' + c.title + '」— ' + c.user_story + ' (核心硬體:' + (c.kebbi_unique_hw || []).join('、') + ';為何非平板:' + c.why_not_tablet + ')'
  ).join('\n')
}
let coordText = ''
for (const g of GROUPS) {
  coordText += '\n● ' + g.id + ' ' + g.level + '(' + g.age + ')\n'
  for (const cat of CATS) {
    coordText += ' ▷ ' + cat + ':\n' + (listFor(byKey[g.id + '|' + cat]) || '  (無候選)') + '\n'
  }
}

const coordPrompt = [
  '你是競賽總教練。下面是 5 組學生 × 三大類(智慧教學/互動遊戲/生活應用)通過對抗式平板測試的候選主題。',
  '請為每一組的每一類各選定 1 個最佳主題(共 15 個),原則:',
  '1. 優先選 [存活] 的;若該格只有待強化候選,選最有潛力的並把 needs_strengthening 設為 true。',
  '2. 強烈要求跨組與跨類差異化:15 個主題要分布在不同領域/情境/機制,避免雷同(例如不要兩組都做手語、都做導覽)。',
  '3. 主題要最能凸顯 Kebbi 移動/關節/聲源定位/多機協作的物理特色,且發展程度適配該組;互動遊戲與高年級組可優先考慮需要多台 Kebbi 的設計。',
  'chosen_title 請從清單中逐字複製。最後在 diversity_summary 說明 10 個主題如何彼此區隔。',
  '',
  '【候選清單】' + coordText,
].join('\n')

const assignResult = await agent(coordPrompt, { label: '跨組指派與去重', phase: 'Assign', schema: ASSIGN_SCHEMA, effort: 'high' })

function resolvePick(a) {
  const slot = byKey[a.group_id + '|' + a.category]
  const exact = candByTitle[a.group_id + '|' + a.category + '|' + a.chosen_title]
  const fallback = slot ? (slot.survivors[0] || slot.failed[0]) : null
  return { assignment: a, candidate: exact || fallback }
}
let picks = (assignResult && assignResult.assignments ? assignResult.assignments : []).map(resolvePick).filter((p) => p.candidate)
const haveKeys = new Set(picks.map((p) => p.candidate.group_id + '|' + p.candidate.category))
for (const g of GROUPS) {
  for (const cat of CATS) {
    const k = g.id + '|' + cat
    if (!haveKeys.has(k)) {
      const slot = byKey[k]
      const c = slot.survivors[0] || slot.failed[0]
      if (c) picks.push({ assignment: { group_id: g.id, category: cat, chosen_title: c.title, rationale: '(系統補選)', needs_strengthening: !slot.survivors.length }, candidate: c })
    }
  }
}

// ---------- Phase 4: 完整展開 ----------
phase('Develop')
function developPrompt(p) {
  const g = GROUPS.find((x) => x.id === p.candidate.group_id)
  const c = p.candidate
  const skeptic = (c.verdicts || []).map((v, i) => '  律師' + (i + 1) + ':可被平板取代=' + v.tablet_can_replicate + ';少了Kebbi失去=' + v.genuinely_lost_without_kebbi).join('\n')
  const strengthen = p.assignment.needs_strengthening
    ? '【注意】此主題對抗測試未完全過關,請在展開時強化其物理依賴(移動/關節/聲源定位),讓它變成平板真的做不到。'
    : ''
  return [
    '你是 Kebbi 競賽資深指導老師。請把以下已通過(或接近通過)對抗式平板測試的主題,展開成一份可直接拿去培訓與參賽的完整提案。',
    '',
    '【組別】' + g.level + '(' + g.age + ')｜【類別】' + p.candidate.category,
    '【主題】' + c.title,
    '【使用者故事】' + c.user_story,
    '【核心 Kebbi 能力】' + (c.kebbi_unique_hw || []).join('、'),
    '【需要 Kebbi 台數】' + (c.kebbi_count || '未註明'),
    '【功能組合】' + c.feature_combo,
    '【流程草稿】' + c.flow,
    '【為何非平板】' + c.why_not_tablet,
    '【平板律師回饋】\n' + (skeptic || '  (無)'),
    strengthen,
    '',
    DOCTRINE,
    '',
    '【輸出要求】markdown 欄位請輸出一個完整小節,以 #### 〔' + p.candidate.category + '〕' + c.title + ' 為標題,接著用粗體標籤條列以下各項(繁體中文):',
    '對象與發展適配(為何適合這組年齡與程式深度)、',
    '真實需求/使用者故事、',
    'Kebbi 功能組合(務必標出哪些是平板沒有的能力:移動/關節/聲源定位/多機協作,並註明需要幾台 Kebbi、若多台則說明各台分工與如何協作)、',
    '運作流程(分步驟,具體可實作)、',
    '為何平板做不到-核心論證(這是評審與差異化關鍵,要寫得無懈可擊)、',
    '對應評分亮點(對應應用性/創意性/挑戰性/完整性/展演)、',
    '挑戰度(★1到5)與程式深度、',
    '可現場 demo 的 MVP(先做哪一條最小流程)、',
    '展演高光時刻(現場 6 分鐘哪個會動/比手勢/轉頭的橋段最吸睛)。',
    '另外回傳 kebbi_unique_hw(一句話列核心硬體)、tablet_proof_one_liner(一句話講為何平板做不到)、challenge(★數)。',
  ].join('\n')
}

const developed = await parallel(picks.map((p) => () =>
  agent(developPrompt(p), {
    label: '展開:' + p.candidate.group_id + '-' + p.candidate.category + ' ' + p.candidate.title,
    phase: 'Develop', schema: DEVELOP_SCHEMA, effort: 'high',
  }).then((d) => (d ? Object.assign({}, d, { group_id: p.candidate.group_id, category: p.candidate.category }) : null))
))
const finalTopics = developed.filter(Boolean)

// ---------- Phase 5: 評審視角體檢 ----------
phase('Critique')
const critiqueInput = finalTopics.map((t) => '- [' + t.group_id + '/' + t.category + '] ' + t.title + ' — 平板免疫理由:' + t.tablet_proof_one_liner + ' (核心硬體:' + t.kebbi_unique_hw + ')').join('\n')
const critic = await agent([
  '你是競賽評審兼嚴格的完整性批評者。以下是 5 組學生最終選定的 10 個 Kebbi 主題。請從評審視角體檢:',
  '1. 每個主題是否真的平板做不到(平板免疫是否成立,有沒有哪個其實平板就能做)?',
  '2. 10 個主題彼此是否夠差異化(機制/領域/情境不重疊)?',
  '3. 發展程度是否適配各組年齡?',
  '4. 是否對齊評分(應用性/創意性/挑戰性/完整性/展演)?',
  '請點名最弱、最該再強化的主題,並給具體改進建議。',
  '',
  critiqueInput,
].join('\n'), { label: '評審視角體檢', phase: 'Critique', schema: CRITIC_SCHEMA, effort: 'high' })

// ---------- 組裝最終文件 ----------
let doc = ''
doc += '# Kebbi 競賽・5 組學生選題（平板做不到・對抗式驗證版）\n\n'
doc += '> 以多代理對抗式驗證產生:每組×三大類(智慧教學/互動遊戲/生活應用)各生成 5 個候選,經兩位平板辯護律師攻擊,兩位都攻不下(平板無法達成同樣成效)才存活。鼓勵採用需多台 Kebbi 協作的設計(最強平板免疫)。\n'
doc += '> 統計:共生成 ' + totalCand + ' 個候選,通過雙重平板測試存活 ' + totalSurv + ' 個。\n\n'
doc += '## 分組對應假設\n'
doc += '| 組 | 程度 | 年齡 | 型態 |\n|---|---|---|---|\n'
for (const g of GROUPS) doc += '| ' + g.id + ' | ' + g.level + ' | ' + g.age + ' | ' + g.students + ' |\n'
doc += '\n## ' + finalTopics.length + ' 個選定主題總覽\n'
doc += '| 組 | ' + CATS.join(' | ') + ' |\n'
doc += '|---|' + CATS.map(() => '---').join('|') + '|\n'
for (const g of GROUPS) {
  let row = '| ' + g.id + ' ' + g.level + ' '
  for (const cat of CATS) {
    const t = finalTopics.find((x) => x.group_id === g.id && x.category === cat)
    row += '| ' + (t ? t.title : '—') + ' '
  }
  doc += row + '|\n'
}
doc += '\n---\n\n## 各組詳細提案\n\n'
for (const g of GROUPS) {
  doc += '### ' + g.id + '｜' + g.level + '（' + g.age + '）\n\n'
  for (const cat of CATS) {
    const t = finalTopics.find((x) => x.group_id === g.id && x.category === cat)
    if (t) doc += t.markdown + '\n\n'
  }
  doc += '---\n\n'
}
doc += '## 評審視角體檢\n\n'
if (critic) {
  doc += '**整體**:' + critic.overall + '\n\n'
  doc += '**跨組差異化**:' + critic.diversity_check + '\n\n'
  doc += '**最該再強化的主題**:\n' + (critic.weakest_topics || []).map((x) => '- ' + x).join('\n') + '\n\n'
  doc += '**具體改進建議**:\n' + (critic.improvements || []).map((x) => '- ' + x).join('\n') + '\n\n'
}
if (assignResult && assignResult.diversity_summary) doc += '**差異化設計說明(指派階段)**:' + assignResult.diversity_summary + '\n'

return { markdown: doc, stats: { totalCand: totalCand, totalSurv: totalSurv }, topicCount: finalTopics.length }
