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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/************************************************************
************************************************************/

#if false
/**************************************************
use Renderer.sharedMaterial
**************************************************/
public class ControlMaterialColor : MonoBehaviour {
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
		SetColor(ref color);
		
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
public class ControlMaterialColor : MonoBehaviour {
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
		SetColor(ref color);
		
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

