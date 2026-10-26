// JSON pequeno, com limite de profundidade/nos e rejeicao de CHAVES REPETIDAS
// inclusive quando escapadas. JSON.parse sozinho perde essa evidencia.
export function parseStrictJson(text) {
  let at=0,nodes=0;
  const whitespace=()=>{while(/[\t\r\n ]/.test(text[at]||"x"))at++;};
  const string=()=>{
    if(text[at]!== '"')throw 0;
    const start=at++;
    while(at<text.length){const c=text[at++];if(c==='"')return JSON.parse(text.slice(start,at));if(c==='\\')at++;}
    throw 0;
  };
  const value=depth=>{
    if(depth>4||++nodes>128)throw 0;
    whitespace();
    if(text[at]==='"')return string();
    if(text[at]==='{'){
      at++;whitespace();const object=Object.create(null),keys=new Set();
      if(text[at]==='}'){at++;return object;}
      for(;;){
        whitespace();const key=string();if(keys.has(key))throw 0;keys.add(key);
        whitespace();if(text[at++]!==':')throw 0;object[key]=value(depth+1);whitespace();
        const end=text[at++];if(end==='}')return object;if(end!==',')throw 0;
      }
    }
    if(text[at]==='['){
      at++;whitespace();const array=[];if(text[at]===']'){at++;return array;}
      for(;;){array.push(value(depth+1));whitespace();const end=text[at++];if(end===']')return array;if(end!==',')throw 0;}
    }
    const token=/^(?:true|false|null|-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?)/.exec(text.slice(at))?.[0];
    if(!token)throw 0;at+=token.length;return JSON.parse(token);
  };
  try{if(typeof text!=="string"||text.length>4096)return null;const parsed=value(0);whitespace();return at===text.length?parsed:null;}
  catch{return null;}
}
