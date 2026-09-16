using System.Collections.Generic;
using UnityEngine;

// 게임에 등장하는 카드 전체 목록. 메뉴 "Greed Bound > 카드 데이터 생성"이 채움
[CreateAssetMenu(menuName = "Greed Bound/Card Library", fileName = "CardLibrary")]
public class CardLibrary : ScriptableObject
{
    public List<CardData> cards = new List<CardData>();
}
