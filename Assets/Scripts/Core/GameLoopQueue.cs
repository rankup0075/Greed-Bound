using System;
using System.Collections.Generic;
using UnityEngine;

// 게임 루프 안에서 처리하는 지연 실행 큐 (Spec 9장 타이머).
// 코루틴·Invoke 대신 이걸 쓰는 이유: 라운드 종료 판정이 "아직 실행 안 된 부활·폭발"이 남았는지 알아야 하기 때문.
// 예) GameLoopQueue.Instance.Schedule(2f, () => Revive(enemy), blocksRoundEnd: true);
[DefaultExecutionOrder(-100)]  // 다른 스크립트보다 먼저 처리해서, 같은 프레임의 판정이 최신 상태를 보게 함
public class GameLoopQueue : MonoBehaviour
{
    public static GameLoopQueue Instance { get; private set; }

    struct Entry
    {
        public float dueTime;
        public long sequence;          // 같은 시각이면 먼저 예약한 것부터
        public Action action;
        public bool blocksRoundEnd;
    }

    // 실행 대기 중인 항목 중 라운드 종료를 막는 것의 수. RoundManager가 전멸 판정에 사용
    public int PendingBlockingCount { get; private set; }
    public int PendingCount => entries.Count;

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Entry> due = new List<Entry>();
    private long nextSequence;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameLoopQueue가 씬에 두 개 이상 있습니다. 하나만 남기세요.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // delay초 뒤에 action 실행. 부활·폭발·예고 피해처럼 "끝나기 전엔 라운드가 끝나면 안 되는" 일은 blocksRoundEnd = true
    public void Schedule(float delay, Action action, bool blocksRoundEnd)
    {
        entries.Add(new Entry
        {
            dueTime = Time.time + delay,
            sequence = nextSequence++,
            action = action,
            blocksRoundEnd = blocksRoundEnd,
        });
        if (blocksRoundEnd) PendingBlockingCount++;
    }

    // 라운드 종료·재시작 시 남은 예약을 모두 버림
    public void Clear()
    {
        entries.Clear();
        PendingBlockingCount = 0;
    }

    void FixedUpdate()
    {
        float now = Time.time;

        due.Clear();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].dueTime > now) continue;
            due.Add(entries[i]);
            entries.RemoveAt(i);
        }
        if (due.Count == 0) return;

        due.Sort((a, b) => a.dueTime != b.dueTime ? a.dueTime.CompareTo(b.dueTime) : a.sequence.CompareTo(b.sequence));

        foreach (Entry entry in due)
        {
            if (entry.blocksRoundEnd) PendingBlockingCount--;
            try
            {
                entry.action?.Invoke();  // 실행 중에 새로 예약해도 됨 (다음 처리 때 반영)
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);  // 하나가 실패해도 나머지는 계속 처리
            }
        }
    }
}
