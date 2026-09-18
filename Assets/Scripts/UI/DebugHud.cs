using UnityEngine;

// 예전 임시 HUD(OnGUI) 자리. 정식 HUD(`GameHud`)로 교체됐고, 씬·프리팹에 남아 있는 이 컴포넌트가
// 실행 시 스스로 GameHud로 바꿔 달기 때문에 씬을 고치지 않아도 된다.
// (씬을 다시 만들 일이 있으면 BattleArenaBuilder·ShopSceneBuilder가 GameHud를 바로 붙인다)
public class DebugHud : MonoBehaviour
{
    void Awake()
    {
        if (GetComponent<GameHud>() == null) gameObject.AddComponent<GameHud>();
        Destroy(this);
    }
}
