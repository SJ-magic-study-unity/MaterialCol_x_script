/************************************************************
参考URL
	マテリアルのプロパティをスクリプトから変更【Unity】
	http://kan-kikuchi.hatenablog.com/entry/Material
		contens
			今回、最も参考にしたpage.
		
	Color (unity official documentation)
	https://docs.unity3d.com/ja/current/ScriptReference/Color.html
		contents
			rgbaは、0.0-1.0である点に注意
			
	Unity でスクリプトから Renderer の Material を操作するとリークする件について
	https://qiita.com/Dameppoi/items/5d43e562aae023ffd79b
	
	Renderer.material(公式page:English)
	https://docs.unity3d.com/ScriptReference/Renderer-material.html
		contents
			It is your responsibility to destroy the materials when the game object is being destroyed. 

tips
	Renderer.sharedMaterialは、元のmaterial自体を操作する。
	よって、同materialをapplyされた全てのObjectが同時に変更される。
	また、scriptによって変更された"元のmaterial"は、実行後も変更されたままである点に注意。
	
	Renderer.materialを操作すると、このObjectのみのmaterialが、自動で複製され、これを操作する。
	GameObjectを破棄しても、materialは破棄されないので、leakが心配。
	But:official pageの使い方に乗っ取り、Start()で取得してこれを操作。OnDestroy()でmaterialを破棄(DestroyImmediate)すれば大丈夫そう。
	
	
study : memory leakは大丈夫か?
	Renderer.material のmemory leakが心配だったので、調査した。
	
	概要
		Renderer.materialのofficial page.
			https://docs.unity3d.com/ja/2017.4/ScriptReference/Renderer-material.html
		より、Resources.FindObjectsOfTypeAll
			https://docs.unity3d.com/ja/current/ScriptReference/Resources.FindObjectsOfTypeAll.html
		を使ってmaterialのResourceを表示しながらtest.
		
	結果と考察
		*	Renderer.materialを使っても、実行中に"Resources.FindObjectsOfTypeAll(typeof(Material)).Length"が増えていくことはなかった。
		*	"Resources.FindObjectsOfTypeAll(typeof(Material)).Length"の絶対値については、Scene中に配置したmaterialの数と合わず、不明
		*	unity editor上では、Start/Stop/Start...を繰り返す度、"Resources.FindObjectsOfTypeAll(typeof(Material)).Length"の初期値が増えていった(実行中には増えないが).
			試しにexeを書き出し、これを実行してみたが、この時は、何度起動し直しても、毎回 同じ初期値(つまりeditorのみの問題).
			Material意外のitem(e.g. GameObject)は、起動の度、増えるようなことは、ない。
			
			そこで、特にmaterialを触るなどせず、単に1つのsphereのみをScene上に配置するのみで、調査を進めた。
			Unityのversion upで入り込んだバグなのか...?
			"Resources.FindObjectsOfTypeAll(typeof(Material)).Length"について、
				Unity2017.4.11f1(Latest of 2017)	までは、起動の度、同じ値(相変わらず、絶対値は不明)
				Unity2018.1.0f2(1st of 2018)		から、起動の度に絶対値が1ずつ増える現象が発生
				
			Unity2018から発生したbug? or editor上で、Gabage Collectionをするタイミングや条件を最適化したのだろうか？
			Unity2017を使うことも1案だが、以下の理由から最新(Unity2018.2.2f1)を使うこととしよう。
				*	少なくとも、書き出されたexeは問題ない
				*	Klak Syphonが"Unity 2018.1"以降対応
				
			「editor上の最適化が異なるだけ」であると信じるとしよう。
************************************************************/

/************************************************************
<Bloom>
PostProcessing Stack v2を使う : bloom
	http://am1tanaka.hatenablog.com/entry/2018/05/19/172121
	
bloom
	http://tsubakit1.hateblo.jp/entry/2018/03/06/224207
************************************************************/

/************************************************************
参考URL
	Setting emission scale in script
	https://forum.unity.com/threads/setting-emission-scale-in-script.297525/
	
tips
	Emissionをscriptから変更したい場合、
		material.SetColor("_EmissionColor", new Color(0.0f,0.7f,1.0f,1.0f));
		material.SetFloat("_EmissionScaleUI", 6.0f);
	としたくなるが、
	実は、shaderは、"_EmissoinColor"しか使用していない。
	
	なので、emissionを操作したい場合は、以下のようにする.
	_EmissionColorに設定したColorの要素が、1より値がOverすると、emissionのintensityがzeroを超え、Objectから光が漏れる。
		float intensity = 5.0f;
		material.EnableKeyword ("_EMISSION");
		material.SetColor("_EmissionColor", new Color(0.0f,0.7f,1.0f,1.0f) * intensity);
		
	また、
		material.EnableKeyword ("_EMISSION");
	は、materialのinspector上にある、emission□ checkに相当する。
	これを操作することで、(何故か)inspector上のそれは、変わらないが、動作上は、checkが入る。
	ちなみに、checkを外すのは、
		material.DisableKeyword ("_EMISSION");
		
	
	ここで、もう1歩踏み込んで考察してみる。
	Renderer.sharedMaterialで元materialが変更され、実行後も値が変わったままであること、を利用して、
	scriptから変更された値をcheck.
		float intensity = 2.0f;
		material.EnableKeyword ("_EMISSION");
		material.SetColor("_EmissionColor", new Color(1.0f,0.0f,0.0f,1.0f) * intensity);
	とすると、
		color		= (191, 0, 0)
		intensity	= 1.416925
	となった。
	要素が"1"を超えると、intensityが"0"でなくなると同時に、Colorも変わる(255 -> 191)ようだ。
	漏れた分も考慮して、いい感じになるよyに、unity側で上手い値を設定してくれているのだろうか？
	
	
	値の絶対値がどのように算出されているか、は気になるが、
	とりあえずは、実機調整でいいだろう。
************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


#if false
/**************************************************
use Renderer.sharedMaterial
**************************************************/
public class ControlEmissionColor : MonoBehaviour {
	/****************************************
	****************************************/
	Material sharedMaterial;
	string label = "";

	/****************************************
	****************************************/
	/******************************
	******************************/
	void Start () {
		sharedMaterial = GetComponent<Renderer>().sharedMaterial;
		print_Resource();
	}
	
	/******************************
	******************************/
	void Update () {
		float freq = 0.5f;
		float val = 1.0f * (Mathf.Sin(2.0f * Mathf.PI * freq * Time.time) + 1.0f) / 2.0f;
		// label =	string.Format("{0:0.000000}",	val);
		
		/********************
		********************/
		Color color = new Color(val, 0, 0, 1.0f);
		// SetColor(ref color);
		Set_EmissionColor(ref color);
		
		/********************
		********************/
		if (Input.GetKeyDown(KeyCode.A)){
			print_Resource();
        }
	}
	
	/******************************
	description
		colorのapplyは、以下のどちらでもok.
		後者の方が柔軟.
			sharedMaterial.color = color;
			sharedMaterial.SetColor("_Color", color);
	******************************/
	void SetColor(ref Color color){
		// sharedMaterial.color = color;
		sharedMaterial.SetColor("_Color", color);
	}
	
	/******************************
	description
		colorのapplyは、以下のどちらでもok.
		後者の方が柔軟.
			material.color = color;
			material.SetColor("_Color", color);
	******************************/
	void Set_EmissionColor(ref Color color){
		float intensity = 5.0f;
		sharedMaterial.EnableKeyword("_EMISSION"); // 
		
		/********************
		全ての要素を少し持ち上げることで、
		明るくなるに連れて、白飛びしていく様子をsimulation.
		********************/
		const float WhiteUp = 0.02f;
		color.r += WhiteUp;
		color.g += WhiteUp;
		color.b += WhiteUp;
		sharedMaterial.SetColor("_EmissionColor", color * intensity);
	}
	
	/******************************
	******************************/
	void OnDestroy(){
		print_Resource();
	}

	/******************************
	******************************/
	void print_Resource(){
        print("Materials " + Resources.FindObjectsOfTypeAll(typeof(Material)).Length);
		label = string.Format("Materials:{0:0}", Resources.FindObjectsOfTypeAll(typeof(Material)).Length);
		
	/*
		print("All " + Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object)).Length);
        print("Textures " + Resources.FindObjectsOfTypeAll(typeof(Texture)).Length);
        print("AudioClips " + Resources.FindObjectsOfTypeAll(typeof(AudioClip)).Length);
        print("Meshes " + Resources.FindObjectsOfTypeAll(typeof(Mesh)).Length);
        print("Materials " + Resources.FindObjectsOfTypeAll(typeof(Material)).Length);
        print("GameObjects " + Resources.FindObjectsOfTypeAll(typeof(GameObject)).Length);
        print("Components " + Resources.FindObjectsOfTypeAll(typeof(Component)).Length);
	*/
	}

	/******************************
	******************************/
	void OnGUI(){
		GUI.Label(new Rect(0, 10, 100, 30), label);
	}
}

#else

/**************************************************
use Renderer.material
**************************************************/
public class ControlEmissionColor : MonoBehaviour {
	/****************************************
	****************************************/
	Material material;
	string label = "";

	/****************************************
	****************************************/
	/******************************
	******************************/
	void Start () {
		material = GetComponent<Renderer>().material;
		print_Resource();
	}
	
	/******************************
	******************************/
	void Update () {
		float freq = 0.5f;
		float val = 1.0f * (Mathf.Sin(2.0f * Mathf.PI * freq * Time.time) + 1.0f) / 2.0f;
		// label =	string.Format("{0:0.000000}",	val);
		
		/********************
		********************/
		Color color = new Color(val, 0, 0, 1.0f);
		// SetColor(ref color);
		Set_EmissionColor(ref color);
		
		/********************
		********************/
		if (Input.GetKeyDown(KeyCode.A)){
			print_Resource();
        }
	}
	
	/******************************
	description
		colorのapplyは、以下のどちらでもok.
		後者の方が柔軟.
			material.color = color;
			material.SetColor("_Color", color);
	******************************/
	void SetColor(ref Color color){
		// material.color = color;
		material.SetColor("_Color", color);
	}
	
	/******************************
	description
		colorのapplyは、以下のどちらでもok.
		後者の方が柔軟.
			material.color = color;
			material.SetColor("_Color", color);
	******************************/
	void Set_EmissionColor(ref Color color){
		float intensity = 3.0f;
		material.EnableKeyword("_EMISSION"); // 
		
		/********************
		全ての要素を少し持ち上げることで、
		明るくなるに連れて、白飛びしていく様子をsimulation.
		********************/
		const float WhiteUp = 0.02f;
		color.r += WhiteUp;
		color.g += WhiteUp;
		color.b += WhiteUp;
		material.SetColor("_EmissionColor", color * intensity);
	}
	
	/******************************
	******************************/
	void OnDestroy(){
		DestroyImmediate(material);
		print_Resource();
	}
	
	/******************************
	******************************/
	void print_Resource(){
        print("Materials " + Resources.FindObjectsOfTypeAll(typeof(Material)).Length);
		label = string.Format("Materials:{0:0}", Resources.FindObjectsOfTypeAll(typeof(Material)).Length);
		
	/*
		print("All " + Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object)).Length);
        print("Textures " + Resources.FindObjectsOfTypeAll(typeof(Texture)).Length);
        print("AudioClips " + Resources.FindObjectsOfTypeAll(typeof(AudioClip)).Length);
        print("Meshes " + Resources.FindObjectsOfTypeAll(typeof(Mesh)).Length);
        print("Materials " + Resources.FindObjectsOfTypeAll(typeof(Material)).Length);
        print("GameObjects " + Resources.FindObjectsOfTypeAll(typeof(GameObject)).Length);
        print("Components " + Resources.FindObjectsOfTypeAll(typeof(Component)).Length);
	*/
	}

	/******************************
	******************************/
	void OnGUI(){
		GUI.Label(new Rect(0, 10, 100, 30), label);
	}
}

#endif


