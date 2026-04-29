using LostMemory.UI;
using UnityEngine;

public class TestHealthBar : MonoBehaviour
{
    [SerializeField] private HealthBarView _view;
    
    void Start()
    {
        _view.UpdateHP(40, 85);   // HP 40/85 → 절반 이하
        _view.UpdateMP(70, 100);  // MP 70/100 → 70%
    }
}