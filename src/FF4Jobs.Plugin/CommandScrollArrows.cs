using Last.UI.KeyInput;
using UnityEngine;
using UnityEngine.UI;

namespace FF4Jobs;

// Copy the battle inventory's native triangle sprites into the command window.
// No prefab controllers/animators are cloned; these indicators never own input.
internal sealed class CommandScrollArrows
{
    private Image? up, down;
    internal void Update(BattleCommandSelectController c, bool above, bool below)
    {
        if(up==null || down==null)
        {
            var parent=c.view?.ContentsParent?.transform;
            if(parent==null) return;
            foreach(var source in c.transform.root.GetComponentsInChildren<BattleItemInfomationView>(true))
            {
                if(source.upperArrow?.sprite==null || source.underArrow?.sprite==null) continue;
                up=Create(parent,source.upperArrow,true);
                down=Create(parent,source.underArrow,false);
                break;
            }
            if(up==null && c.pagerController?.view?.leftArrowImage?.sprite!=null)
            {
                var source=c.pagerController.view.leftArrowImage;
                up=Create(parent,source,true,-90);
                down=Create(parent,source,false,90);
            }
            if(up!=null && down!=null)
            {
                // Native arrows can use different pivots and rotations. Align
                // their displayed centers, moving only the upper arrow's X.
                var upper=up.rectTransform;
                var lower=down.rectTransform;
                var upperCenter=parent.InverseTransformPoint(upper.TransformPoint(
                    new Vector3(upper.rect.center.x,upper.rect.center.y,0)));
                var lowerCenter=parent.InverseTransformPoint(lower.TransformPoint(
                    new Vector3(lower.rect.center.x,lower.rect.center.y,0)));
                var position=upper.anchoredPosition;
                position.x+=lowerCenter.x-upperCenter.x;
                upper.anchoredPosition=position;
            }
        }
        if(up!=null) up.gameObject.SetActive(above);
        if(down!=null) down.gameObject.SetActive(below);
    }
    private static Image Create(Transform parent, Image source, bool upper, float rotation=0)
    {
        var go=new GameObject(upper?"FF4 Jobs scroll up":"FF4 Jobs scroll down");
        var rect=go.AddComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(1,upper?1:0);
        rect.pivot=new Vector2(1,upper?1:0);
        rect.anchoredPosition=new Vector2(-4,upper?-4:4);
        rect.sizeDelta=new Vector2(source.rectTransform.rect.width,source.rectTransform.rect.height);
        go.AddComponent<LayoutElement>().ignoreLayout=true;
        var image=go.AddComponent<Image>(); image.sprite=source.sprite;
        image.color=Color.white; image.preserveAspect=true; image.raycastTarget=false;
        rect.localEulerAngles=source.rectTransform.localEulerAngles+new Vector3(0,0,rotation);
        rect.localScale=source.rectTransform.localScale;
        rect.SetAsLastSibling();
        return image;
    }
    internal void Hide()
    {
        if(up!=null) up.gameObject.SetActive(false);
        if(down!=null) down.gameObject.SetActive(false);
    }
    internal void Destroy()
    {
        if(up!=null) UnityEngine.Object.Destroy(up.gameObject);
        if(down!=null) UnityEngine.Object.Destroy(down.gameObject);
        up=down=null;
    }
}
