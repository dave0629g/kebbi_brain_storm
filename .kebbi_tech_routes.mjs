export const meta = {
  name: 'kebbi-tech-routes',
  description: '依實測 SDK 為各隊主攻產生技術實作路線,並對抗式檢查不偷用被擋/未驗能力',
  phases: [
    { title: 'Route', detail: '每隊產生依實測 SDK 的技術實作路線' },
    { title: 'Audit', detail: 'SDK 實測審查官對抗式檢查合規性' },
    { title: 'Revise', detail: '依審查修正成定稿路線' },
  ],
}

const SDK = [
  '【已驗證 SDK 事實(必須嚴格遵守,2026-06-18 反編譯+H201 真機實測)】',
  '硬體前提:競賽展演用「輪式會動的 Kebbi」(會移動);開發機是 H201(桌上型、不會移動,所以移動/多機只能在輪式機與工作坊現場測);共 3 台以上實體機可用。',
  '',
  '【免授權、可直接用(sync 讀取/事件/網路)】',
  '- DOA 聲源角度:getDirectionOfDOA():float(連續度數)+ 事件 onWakeup(...,float direction)/onFaceSpeaker(float)。onWakeup 已 H201 實測。⚠️解析度/正後方/非語音聲音是否更新=未驗。',
  '- 關節 10 馬達(頭2+雙臂8):setMotorPositionInDegree 設角、getMotorPresentPossitionInDegree 讀角,實測可用。逐幀動作、手臂指向、頭部轉向都靠它。',
  '- 對外網路:UnityWebRequest 任意 HTTPS(LLM、雲端語音),無沙箱白名單;UDP 送已實測。',
  '',
  '【Active-code 授權牆:沒授權時 async 命令會「靜默 no-op」,不可依賴】',
  '- 被擋風險:turnToDOA、內建 TTS、motionPlay、LED。',
  '- Workaround 心法(必須採用):①轉向=讀 DOA→自己 setMotor 把 NECK_Z 轉到該角(不用 turnToDOA);②說話=雲端 TTS→Unity AudioSource(不用內建 TTS);③動作=逐幀 setMotor(不用 motionPlay)。',
  '- ⚠️移動命令(move/turn 速度)是否也被授權牆擋=未驗,列為必測。',
  '',
  '【限制/未驗(用到就必須附「先實測 + 單機降級備案」)】',
  '- 移動:只有連續速度 move(m/s)/turn(deg/s),max ~0.2m/s、~30deg/s;❌無指定距離/角度/座標、無里程計、無避障、無 SLAM goto。走位只能「速度×時間」開迴路 + 地面膠帶標記 + 現場排練校時,不能精準自動導航。',
  '- 相機:Nuwa 不給像素;改用 Unity WebCamTexture 自取影像跑自己的 MediaPipe;⚠️Kebbi OS 是否准一般 app 開相機=未驗。',
  '- 麥克風 PCM:SDK 不給;自己用 Android AudioRecord;⚠️麥克風單一擁有者,要先 stopListen 釋放,「系統持麥時能否搶麥」=未驗(G4 第一必測)。',
  '- 印尼語:內建❌(僅 7 語);走雲端(Azure/Google 印尼語 STT/TTS)經 UnityWebRequest;依賴上面「搶麥」+ AudioSource 播放。',
  '- 多機 Kebbi↔Kebbi:非公開 ConnectionManager(WiFi WebSocket P2P)Unity glue 不完整(缺 startListen/send);純 socket 送已驗、收未驗;⚠️從未雙機實跑 → 需補 Java glue + 雙機收送實測。',
  '- 人體 pose/skeleton:SDK ❌;要骨架自己 MediaPipe;臉框中心可推方位、臉框大小粗估距離。',
  '',
  '【鐵律】路線只能建立在「已驗證可用」能力上;凡用到「被擋 async」必須改 workaround;凡用到「未驗能力」(相機/搶麥/多機/移動是否被擋)必須明列「先做的真機實測」與「測失敗時的單機降級備案」。不得假設精準導航。',
].join('\n')

const TEAMS = [
  { id: 'G1', name: 'G1 小五(1人)', flagship: '雙機接力闖關:教室地圖程式設計遊戲',
    mech: '2 台機器人在地面接力走位 + Blockly 程式 + 交棒握手', risk: '移動(開迴路)+多機通訊(未驗)' },
  { id: 'G2', name: 'G2 國二', flagship: '幾何證明接力站:兩機帶讀證明每一步',
    mech: '甲機走到地面大圖邊/角用手臂指認 + 乙機宣告理由 + 雙機接力', risk: '移動走位(開迴路)+手臂指向+多機' },
  { id: 'G3', name: 'G3 國三升高一', flagship: 'Kebbi 體育課:動作鏡像教練',
    mech: '逐幀關節示範 + 聽到發問用 DOA 轉頭面向該生 + 攝影機評學生動作', risk: '關節(已驗)+DOA(已驗)+相機MediaPipe(未驗)' },
  { id: 'G4', name: 'G4 高一升高二', flagship: 'Tebak Arah:印尼語方位定向遊戲',
    mech: 'DOA 當方位對錯真值 + 頭轉面向學生 + 印尼語問答 + LLM 出題糾錯', risk: 'DOA(已驗)+搶麥(未驗)+雲端印尼語+LLM' },
  { id: 'G5', name: 'G5 高三升大學', flagship: '歷史法庭辯論劇場',
    mech: '兩機分飾兩方、中央移動逼近對峙 + DOA 轉向發言學生 + 手臂手勢 + LLM 生成辯詞', risk: '移動逼近(開迴路)+多機+DOA+LLM' },
  { id: 'EGG', name: '合體彩蛋', flagship: 'G2×G3×G5 多機協作大劇場(G5 中控)',
    mech: '3+ 台 Kebbi 由 G5 中控廣播 cue、各站接力演出、壓軸同步走位', risk: '多機通訊(最高風險,未驗)+移動' },
]

const ROUTE_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    markdown: { type: 'string' },
    sdk_calls_used: { type: 'array', items: { type: 'string' } },
    workarounds_used: { type: 'array', items: { type: 'string' } },
    pretests: { type: 'array', items: { type: 'string' } },
    fallback: { type: 'string' },
  },
  required: ['markdown', 'sdk_calls_used', 'workarounds_used', 'pretests', 'fallback'],
}

const AUDIT_SCHEMA = {
  type: 'object', additionalProperties: false,
  properties: {
    compliant: { type: 'boolean' },
    violations: { type: 'array', items: { type: 'string' } },
    must_fix: { type: 'array', items: { type: 'string' } },
  },
  required: ['compliant', 'violations', 'must_fix'],
}

function routePrompt(t) {
  return [
    '你是 Kebbi Unity SDK 資深工程師。請為以下競賽隊伍的主攻主題,寫一份「依實測 SDK 的技術實作路線」。',
    '',
    '【隊伍】' + t.name + '｜主攻:' + t.flagship,
    '【核心機制】' + t.mech,
    '【主要風險】' + t.risk,
    '',
    SDK,
    '',
    '【輸出 markdown(繁中)】以「### ' + t.name + '｜技術實作路線」為標題,包含:',
    '1. **系統架構**:資料流(感測→判斷→動作/語音回饋),標明每步用哪個已驗證 SDK 呼叫或哪條雲端/Unity 路。',
    '2. **關鍵 SDK 呼叫**:逐項列出(setMotorPositionInDegree、getDirectionOfDOA、UnityWebRequest…),並標哪些是免授權同步/事件。',
    '3. **繞授權牆 workaround**:明確寫出用「讀DOA+自寫馬達轉頭/雲端TTS+AudioSource/逐幀setMotor」取代被擋的 async 命令。',
    '4. **移動(若有)**:說明用「速度×時間 + 地面膠帶標記 + 排練校時」開迴路走位,並承認無精準導航;標註只能在輪式機/工作坊測。',
    '5. **多機(若有)**:說明通訊方案(先試純 socket UDP/TCP 自寫,ConnectionManager 需補 Java glue),並把「雙機收送實測」列為前置。',
    '6. **必做真機實測**:列出此隊上線前要先驗的未知(相機權限/搶麥/移動是否被授權牆擋/多機收送/DOA 解析度),每項配一句怎麼測。',
    '7. **單機/降級備案**:任一未驗能力失敗時,如何退成仍能 demo 的版本(呼應決賽完整性)。',
    '8. **學生分工與難度**:依該年齡程式深度分模組。',
    '另外回傳:sdk_calls_used、workarounds_used、pretests、fallback。',
    '嚴禁:依賴被擋 async 而無 workaround;假設精準導航;用到未驗能力卻沒寫實測+備案。',
  ].join('\n')
}

function auditPrompt(t, route) {
  return [
    '你是「Kebbi SDK 實測審查官」,任務是嚴格挑出以下技術路線有沒有違反實測事實。',
    '',
    SDK,
    '',
    '【被審路線】' + t.name + '：' + t.flagship,
    route.markdown,
    '',
    '【逐項檢查並回報】',
    '- 有沒有依賴「被授權牆擋的 async 命令(turnToDOA/內建TTS/motionPlay/LED)」卻沒改 workaround?',
    '- 有沒有用到「未驗能力(相機權限/搶麥/多機收送/移動是否被擋/DOA解析度/360°)」卻沒附「真機實測+單機降級備案」?',
    '- 有沒有假設「精準導航/指定距離或座標/里程計/避障/SLAM」?(SDK 沒有,只有速度)',
    '- 多機題:有沒有把「雙機從未實跑」這個風險講清楚並前置實測?',
    'compliant=是否全部合規;violations=列出違規處;must_fix=具體修正指示。從嚴判定。',
  ].join('\n')
}

function revisePrompt(t, route, audit) {
  return [
    '你是 Kebbi Unity SDK 資深工程師。以下技術路線經審查官發現問題,請修正成定稿。',
    '',
    SDK,
    '',
    '【原路線】\n' + route.markdown,
    '',
    '【審查官 must_fix】\n' + (audit.must_fix || []).map((x) => '- ' + x).join('\n'),
    '【審查官 violations】\n' + (audit.violations || []).map((x) => '- ' + x).join('\n'),
    '',
    '請輸出修正後的完整 markdown(同樣以「### ' + t.name + '｜技術實作路線」為標題,結構同原本),確保:不依賴被擋 async(改 workaround)、所有未驗能力都有實測+降級備案、不假設精準導航。並回傳 sdk_calls_used、workarounds_used、pretests、fallback。',
  ].join('\n')
}

phase('Route')
const results = await pipeline(
  TEAMS,
  (t) => agent(routePrompt(t), { label: '路線:' + t.id, phase: 'Route', schema: ROUTE_SCHEMA, effort: 'high' }),
  (route, t) => {
    if (!route) return null
    return agent(auditPrompt(t, route), { label: '審查:' + t.id, phase: 'Audit', schema: AUDIT_SCHEMA, effort: 'high' })
      .then((audit) => ({ t, route, audit }))
  },
  (prev) => {
    if (!prev) return null
    const { t, route, audit } = prev
    if (audit && audit.compliant && (!audit.must_fix || audit.must_fix.length === 0)) {
      return { t, route, audit, revised: route }
    }
    return agent(revisePrompt(t, route, audit || { must_fix: [], violations: [] }), { label: '修正:' + t.id, phase: 'Revise', schema: ROUTE_SCHEMA, effort: 'high' })
      .then((revised) => ({ t, route, audit, revised: revised || route }))
  }
)

const ok = results.filter(Boolean)

// 組裝
let doc = ''
doc += '# 各隊技術實作路線（依實測 Kebbi SDK｜對抗式審查後定稿）\n\n'
doc += '> 硬體前提:競賽用會移動的輪式 Kebbi、共 3+ 台;開發機 H201 不會動(移動/多機需在輪式機與工作坊現場測)。\n'
doc += '> 每條路線只建立在已驗證能力上,用 workaround 繞 active-code 授權牆,未驗能力一律附「真機實測 + 單機降級備案」。\n\n'

doc += '## 全隊必做真機實測總表（優先在工作坊/輪式機上做）\n'
doc += '| 隊 | 必測項目 |\n|---|---|\n'
for (const r of ok) {
  const pts = (r.revised.pretests || []).join('；')
  doc += '| ' + r.t.id + ' | ' + (pts || '—') + ' |\n'
}
doc += '\n---\n\n'

for (const r of ok) {
  doc += r.revised.markdown + '\n\n'
  doc += '**降級備案**:' + (r.revised.fallback || '—') + '\n\n'
  if (r.audit && !r.audit.compliant) {
    doc += '> 審查修正紀錄:' + ((r.audit.violations || []).join('；') || '已修正') + '\n\n'
  }
  doc += '---\n\n'
}

return { markdown: doc, teams: ok.length }
