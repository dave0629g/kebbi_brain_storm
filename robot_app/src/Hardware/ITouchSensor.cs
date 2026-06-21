using System;

namespace KebbiBrain.Hardware
{
    // 觸控感測抽象(凱比身上的觸控點),對映 NuwaSDK onTouchEvent 的觸控區。
    // 動機:觸控是最被忽略的輸入——給不擅言語/低齡/特殊需求學生「碰觸而非開口」的極低門檻互動,
    //       也可在對話/多機當「換你講」的實體交棒鍵(取代螢幕按鈕/網路 token)。
    // ⚠ Real 端(真凱比 registerRobotEventListener/onTouchEvent)是否受 active-code 授權牆 silent fail 須真機驗(新必測項);
    //   投入 Real 前先用參考專案 ~/Projects/UnityKebbi 的 touching.sh 在真機確認事件可達。互動邏輯本身全程 Sim 可測。
    public enum TouchZone { None, Head, Belly, HandLeft, HandRight }

    public interface ITouchSensor
    {
        void OnTouch(Action<TouchZone> handler);  // 事件訂閱:每次觸控回呼(訂閱後該事件不再進 Poll 佇列)
        bool Poll(out TouchZone zone);            // 輪詢:取出一個待處理觸控,無 → false
    }

    // 觸控 → 語意意圖(純函式,可斷言)。
    public enum TouchMeaning { None, PositiveFeedback, Handoff, Playful }

    public static class TouchIntents
    {
        public static TouchMeaning Meaning(TouchZone z)
        {
            switch (z)
            {
                case TouchZone.Head: return TouchMeaning.PositiveFeedback;   // 摸頭=被肯定/安撫
                case TouchZone.HandLeft:
                case TouchZone.HandRight: return TouchMeaning.Handoff;        // 握手=換你講(實體交棒)
                case TouchZone.Belly: return TouchMeaning.Playful;           // 摸肚子=逗趣
                default: return TouchMeaning.None;
            }
        }
    }

    // 「握手交棒」floor token:取代網路 token/螢幕按鈕。握手(Hand)且我持有發言權 → 把發言權交給對方。
    public sealed class TouchTurnToken
    {
        public bool HasFloor { get; private set; }
        public int Passes { get; private set; }
        public TouchTurnToken(bool startWithFloor = false) { HasFloor = startWithFloor; }

        // 處理一個觸控:握手且我持有發言權 → 釋出(交棒),回 true=「剛交出發言權」。其餘不動作回 false。
        public bool OnTouch(TouchZone z)
        {
            if ((z == TouchZone.HandLeft || z == TouchZone.HandRight) && HasFloor)
            { HasFloor = false; Passes++; return true; }
            return false;
        }

        public void Grab() { HasFloor = true; }   // 對方交棒給我 / 我被指定發言
    }
}
