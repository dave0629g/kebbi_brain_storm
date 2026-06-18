export const meta = {
  name: 'kebbi-indonesian-g4',
  description: 'G4高一升高二 凱比印尼語沉浸 智慧教學主題,對抗Duolingo律師確保平板做不到',
  phases: [
    { title: 'Generate', detail: '四個具身角度(TPR動作/空間走位/聲源定位多人/雙機對話接力)各生成候選' },
    { title: 'TabletTest', detail: 'Duolingo律師+現成裝置律師雙重對抗,雙雙攻不下才存活' },
    { title: 'Judge', detail: '用編號挑出最強3個彼此不同的主題' },
    { title: 'Develop', detail: '完整展開3個主題' },
  ],
}

const DOCTRINE = [
  '【鐵則・平板/Duolingo 測試】平板上的語言 App(Duolingo、Google 翻譯、口說 App)已能做:單字卡、TTS 發音、ASR 口說評分、AI 情境對話、測驗計分。',
  '因此「Kebbi 念印尼語/評發音/語音問答/翻譯/情境對話」這類功能,平板都做得到,一律不算 Kebbi 的特色,也常被律師打掉。',
  '要讓「學印尼語」變成平板做不到,作品必須以 Kebbi 的身體能力為核心機制(拿掉它就垮):',
  '  1) 關節手勢示範 + 攝影機檢查:用身體做 TPR 全身反應動作教學,Kebbi 邊說印尼語邊用手臂與頭做動作,學生模仿,攝影機判斷學生做對沒。',
  '  2) 移動 + 情境/方位走位:Kebbi 在教室實體移動、繞行、帶學生走位,把語意綁進空間與本體感覺。',
  '  3) 聲源定位 + 轉向:多位學生課堂中,Kebbi 判斷誰在用印尼語發言、轉頭面向他,做對話輪替、點名接龍、分組互動。',
  '  4) 雙機對話接力(2 台 Kebbi):兩台 Kebbi 扮演印尼語對話雙方,在空間中互相對話、轉向彼此與學生,學生觀察真實語用後加入三方對話。',
  '本主題由 G4 高一升高二「獨立」進行(最多 2 台 Kebbi,屬 G4 自己的設備),不併入 G2/G3/G5 的跨組多機彩蛋。',
  '凡是拿掉身體/移動/聲源定位/雙機後,用平板就能達到同樣學習成效的點子,一律不合格。',
].join('\n')

const G4 = {
  level: '高一升高二', age: '15-16歲',
  traits: '學科加深、能寫較完整 Python 並串接雲端 AI/翻譯/LLM API、適合有資料與系統感的作品、喜歡有挑戰與成就感',
  prog: 'Python + 串接雲端 AI/翻譯/語音 API + 機器人動作與底盤編程',
}

const HW_ENUM = ['移動', '關節手勢', '聲源定位', '實體在場', '多機協作']

const ANGLES = [
  { key: 'TPR體感動作詞', desc: '用 Kebbi 身體示範印尼語動作詞/形容詞(berdiri 站/duduk 坐/lompat 跳/makan 吃/cepat 快/lambat 慢),學生模仿,攝影機檢查動作對不對。核心硬體=關節手勢。' },
  { key: '空間方位走位', desc: 'Kebbi 物理移動到學生的左/右/前/後並用手臂指方向,教 kiri/kanan/depan/belakang,學生依印尼語指令走位,攝影機判定相對方位。核心硬體=移動。' },
  { key: '聲源定位多人輪替', desc: '多位學生的課堂,Kebbi 用方向性麥克風判斷誰在用印尼語發言並轉頭面向他,做問答輪替、點名接龍(Siapa? 誰?)、分組對話。核心硬體=聲源定位+轉向。需要多位學生才成立。' },
  { key: '雙機對話接力', desc: '2 台 Kebbi 扮演印尼語對話雙方(如顧客 vs 市場老闆、兩個朋友),在空間中互相對話、轉向彼此,示範真實語用與語調,學生觀察後加入三方對話。核心硬體=多機協作。' },
]

const CAND_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    candidates: {
      type: 'array', minItems: 3, maxItems: 3,
      items: {
        type: 'object', additionalProperties: false,
        properties: {
          title: { type: 'string' },
          user_story: { type: 'string' },
          kebbi_unique_hw: { type: 'array', minItems: 1, items: { type: 'string', enum: HW_ENUM } },
          kebbi_count: { type: 'string' },
          indonesian_content: { type: 'string', description: '會教到的印尼語內容範例(單字/句型)' },
          feature_combo: { type: 'string' },
          flow: { type: 'string' },
          why_not_tablet: { type: 'string' },
        },
        required: ['title', 'user_story', 'kebbi_unique_hw', 'kebbi_count', 'indonesian_content', 'feature_combo', 'flow', 'why_not_tablet'],
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

const JUDGE_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    picks: {
      type: 'array', minItems: 1, maxItems: 3,
      items: {
        type: 'object', additionalProperties: false,
        properties: {
          chosen_index: { type: 'integer', description: '清單編號(1 起算)' },
          is_flagship: { type: 'boolean' },
          rationale: { type: 'string' },
        },
        required: ['chosen_index', 'is_flagship', 'rationale'],
      },
    },
    summary: { type: 'string' },
  },
  required: ['picks', 'summary'],
}

const DEVELOP_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    title: { type: 'string' },
    markdown: { type: 'string' },
    tablet_proof_one_liner: { type: 'string' },
    challenge: { type: 'string' },
  },
  required: ['title', 'markdown', 'tablet_proof_one_liner', 'challenge'],
}

function genPrompt(angle) {
  return [
    '你是 Kebbi 凱比機器人競賽的資深語言教學選題教練。請以「' + angle.key + '」這個角度,為 G4 高一升高二的學生,發想剛好 3 個「透過 Kebbi 學印尼語」的智慧教學候選主題。',
    '',
    '【角度說明】' + angle.desc,
    '',
    '【學生組別】程度:' + G4.level + '(' + G4.age + ');特性:' + G4.traits + ';程式深度:' + G4.prog,
    '【競賽脈絡】STEM人文跨域與AI應用競賽,評分重應用性/創意性/挑戰性,決賽看 Kebbi 整體成果與現場實體展演。印尼語為新南向重點語言,也是台灣新住民語文。',
    '',
    DOCTRINE,
    '',
    '【任務要求】',
    '1. 每個候選都必須以本角度的核心硬體為關鍵機制(拿掉就垮)。',
    '2. 不要提出 Kebbi 念單字/評發音/語音問答/純情境對話這種 Duolingo 就能做的點子。',
    '3. 適配 G4 程度(可串接雲端翻譯/LLM、寫 Python、編機器人動作與底盤)。',
    '4. 3 個候選彼此不同。請在 indonesian_content 給出會教到的具體印尼語範例(附中文)。',
    '請輸出 3 個候選,每個含:title、user_story、kebbi_unique_hw、kebbi_count、indonesian_content、feature_combo、flow、why_not_tablet。',
  ].join('\n')
}

function skepticBase(c) {
  return [
    '【被檢驗的主題】',
    '- 主題:' + c.title,
    '- 使用者故事:' + c.user_story,
    '- 印尼語內容:' + c.indonesian_content,
    '- 宣稱用到的 Kebbi 能力:' + (c.kebbi_unique_hw || []).join('、') + ';台數:' + (c.kebbi_count || '未註明'),
    '- 功能組合:' + c.feature_combo,
    '- 運作流程:' + c.flow,
    '- 作者主張為何平板做不到:' + c.why_not_tablet,
  ].join('\n')
}

function skepticDuolingo(c) {
  return [
    '你是「Duolingo 辯護律師」,任務是用最強的平板/手機語言學習 App,證明以下 Kebbi 印尼語主題其實不需要機器人。',
    '',
    skepticBase(c),
    '',
    '【關鍵事實】平板語言 App 已有:單字卡、圖片、TTS 印尼語發音、ASR 口說評分、AI 對話、情境動畫、測驗、攝影機(可拍學生)。',
    '【你的工作】設計一個盡可能完整的平板印尼語 App 試圖達成同樣學習成效。然後嚴格判定:',
    '- tablet_can_replicate:平板能否達成實質相同的印尼語學習成效?預設傾向 true。只有當成效真的依賴 Kebbi 用身體示範動作/在空間移動帶學生走位/用方向性麥轉頭面向發言者/兩台機器人在空間對話接力,且少了它學習成效會明顯改變時,才填 false。',
    '- tablet_counter_design:你的平板方案具體怎麼做。',
    '- genuinely_lost_without_kebbi:若真有,少了 Kebbi 的身體會具體失去什麼(沒有就寫無實質損失)。',
    '- confidence。',
    '務必嚴格、挑剔,不要被作者說服。',
  ].join('\n')
}

function skepticDevices(c) {
  return [
    '你是「現成裝置律師」,主張用智慧音箱(會說印尼語、語音互動、基本聲音處理)+ 手機視訊 + 平板 App 的組合就能取代以下 Kebbi 印尼語主題。',
    '',
    skepticBase(c),
    '',
    '【你的工作】用上述現成裝置設計替代方案,並嚴格判定:',
    '- tablet_can_replicate:現成裝置能否達成實質相同的印尼語學習成效?預設傾向 true,除非成效真的依賴 Kebbi 會用身體示範動作、在空間移動帶學生、轉身面向學生、或兩台實體機器人在空間對話接力。',
    '- tablet_counter_design、genuinely_lost_without_kebbi、confidence 照填。',
    '務必嚴格。',
  ].join('\n')
}

// ---------- 生成 → 對抗測試 ----------
phase('Generate')
const tested = await pipeline(
  ANGLES,
  (angle) => agent(genPrompt(angle), { label: '生成:' + angle.key, phase: 'Generate', schema: CAND_SCHEMA, effort: 'medium' }),
  (gen, angle) => {
    const cs = gen && gen.candidates ? gen.candidates : []
    const thunks = cs.map((c, ci) => () =>
      parallel([
        () => agent(skepticDuolingo(c), { label: 'Duolingo律師:' + angle.key + '#' + (ci + 1), phase: 'TabletTest', schema: VERDICT_SCHEMA, effort: 'medium' }),
        () => agent(skepticDevices(c), { label: '現成裝置律師:' + angle.key + '#' + (ci + 1), phase: 'TabletTest', schema: VERDICT_SCHEMA, effort: 'medium' }),
      ]).then((verdicts) => {
        const vs = verdicts.filter(Boolean)
        const survives = vs.length === 2 && vs.every((v) => v.tablet_can_replicate === false)
        return Object.assign({}, c, { angle: angle.key, verdicts: vs, survives: survives })
      })
    )
    return parallel(thunks).then((results) => results.filter(Boolean))
  }
)

const allCand = tested.filter(Boolean).flat()
const survivors = allCand.filter((c) => c.survives)
log('印尼語候選 ' + allCand.length + ' 個,通過雙重平板測試存活 ' + survivors.length + ' 個')

// ---------- Judge: 用編號挑 3 個最強且不同 ----------
phase('Judge')
const pool = survivors.length >= 3 ? survivors : allCand
const judgeList = pool.map((c, i) => (i + 1) + '. 「' + c.title + '」[' + (c.survives ? '存活' : '未過') + '|角度:' + c.angle + '|硬體:' + (c.kebbi_unique_hw || []).join('、') + '] ' + c.user_story).join('\n')
const judge = await agent([
  '你是競賽總教練。以下是「Kebbi 學印尼語」候選主題(G4 高一升高二獨立主題),每個前面有編號。',
  '請挑出最強、彼此最不同的 3 個(分屬不同角度、不同核心硬體、不同印尼語內容更好),其中標一個為主攻 flagship。',
  '請用 chosen_index 回傳清單編號(1 起算),三個編號必須相異。優先選標記[存活]者。',
  '',
  judgeList,
].join('\n'), { label: '挑選3個主題', phase: 'Judge', schema: JUDGE_SCHEMA, effort: 'high' })

let chosen = (judge && judge.picks ? judge.picks : [])
  .map((p) => ({ pick: p, candidate: pool[p.chosen_index - 1] }))
  .filter((x) => x.candidate)
const seenTitle = new Set()
chosen = chosen.filter((x) => { if (seenTitle.has(x.candidate.title)) return false; seenTitle.add(x.candidate.title); return true })
// 補滿至最多 3 個不同候選
for (const c of pool) {
  if (chosen.length >= 3) break
  if (!seenTitle.has(c.title)) { seenTitle.add(c.title); chosen.push({ pick: { is_flagship: false, rationale: '(補充)' }, candidate: c }) }
}
if (!chosen.length && pool.length) chosen = [{ pick: { is_flagship: true, rationale: '(系統補選)' }, candidate: pool[0] }]

// ---------- Develop ----------
phase('Develop')
function developPrompt(x) {
  const c = x.candidate
  const skeptic = (c.verdicts || []).map((v, i) => '  律師' + (i + 1) + ':可被平板取代=' + v.tablet_can_replicate + ';少了Kebbi失去=' + v.genuinely_lost_without_kebbi).join('\n')
  const strengthen = c.survives ? '' : '【注意】此主題對抗測試未完全過關,展開時請強化身體/移動/聲源定位/雙機依賴,讓它變成平板真的做不到。'
  return [
    '你是 Kebbi 競賽資深印尼語教學指導老師。請把以下主題展開成可直接培訓與參賽的完整提案(G4 高一升高二獨立主題)。',
    '',
    '【主題】' + c.title + (x.pick.is_flagship ? '(主攻 flagship)' : '(備案/延伸)'),
    '【角度】' + c.angle,
    '【使用者故事】' + c.user_story,
    '【印尼語內容】' + c.indonesian_content,
    '【核心 Kebbi 能力】' + (c.kebbi_unique_hw || []).join('、') + ';台數:' + (c.kebbi_count || '1-2台'),
    '【功能組合】' + c.feature_combo,
    '【流程草稿】' + c.flow,
    '【為何非平板】' + c.why_not_tablet,
    '【平板律師回饋】\n' + (skeptic || '  (無)'),
    strengthen,
    '',
    DOCTRINE,
    '',
    '【輸出要求】markdown 欄位請輸出一個完整小節,以 #### ' + c.title + ' 為標題,用粗體標籤條列(繁體中文):',
    '對象與發展適配、',
    '真實需求/使用者故事、',
    '會教到的印尼語內容(列具體單字/句型,附中文)、',
    'Kebbi 功能組合(標出哪些是平板沒有的能力,需要幾台 Kebbi)、',
    '運作流程(分步驟可實作)、',
    '為何平板/Duolingo 做不到-核心論證(無懈可擊)、',
    '對應評分亮點(應用性/創意性/挑戰性/完整性/展演)、',
    '挑戰度(★1到5)與程式深度(可串接哪些 API)、',
    '可現場 demo 的 MVP、',
    '展演高光時刻(現場哪個用身體示範/移動/聲源定位/雙機的橋段最吸睛)。',
    '另回傳 tablet_proof_one_liner、challenge(★數)。',
  ].join('\n')
}

const developed = await parallel(chosen.map((x) => () =>
  agent(developPrompt(x), { label: '展開:' + x.candidate.title, phase: 'Develop', schema: DEVELOP_SCHEMA, effort: 'high' })
    .then((d) => (d ? Object.assign({}, d, { is_flagship: x.pick.is_flagship, angle: x.candidate.angle }) : null))
))
const finalTopics = developed.filter(Boolean)

// ---------- 組裝 ----------
let doc = ''
doc += '# Kebbi 印尼語沉浸（G4 高一升高二・獨立主題・平板做不到）\n\n'
doc += '> 智慧教學類。G4 獨立進行,不併入 G2/G3/G5 跨組多機彩蛋(最多 2 台 Kebbi,屬 G4 自有設備)。\n'
doc += '> 對抗式驗證:每個候選都要過「Duolingo 律師」與「現成裝置律師」雙重攻擊,平板/語言App 無法達成同樣成效才存活。\n'
doc += '> 統計:生成 ' + allCand.length + ' 個候選,存活 ' + survivors.length + ' 個,精選展開 ' + finalTopics.length + ' 個。\n\n'
doc += '## 為何「學印尼語」不能只靠平板(差異化主張)\n'
doc += 'Duolingo 等平板語言 App 已能念印尼語、評發音、做對話測驗;因此本主題的價值必須來自 Kebbi 平板沒有的身體能力——**用關節手勢示範動作、在空間移動帶學生走位、用方向性麥轉頭面向發言者、兩台機器人在空間對話接力**。這些把「印尼語」與身體、空間、真人般的互動綁在一起,產生平板無法複製的沉浸與肌肉記憶。\n\n'
doc += '## 精選主題(擇一主攻,其餘為備案/延伸)\n\n'
const flagship = finalTopics.filter((t) => t.is_flagship)
const others = finalTopics.filter((t) => !t.is_flagship)
for (const t of flagship.concat(others)) doc += t.markdown + '\n\n'
if (judge && judge.summary) doc += '---\n\n**總教練評語**:' + judge.summary + '\n'

return { markdown: doc, stats: { total: allCand.length, survived: survivors.length, developed: finalTopics.length } }
