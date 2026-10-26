// Exemplo independente, NAO provisionado/implantado. O cron do proprio receptor
// nao pode detectar sua ausencia; este observador precisa de agendamento separado.
import {readHealth,logHealth} from "./observability.mjs";
export default {async scheduled(_event,env){
  if(env.OBSERVABILITY_LOG_ENABLED!=="yes")return;
  try{logHealth(await readHealth(env.DB,Math.floor(Date.now()/1000),env.GITHUB_TOKEN_EXPIRES_AT),"yes");}
  catch{console.log(JSON.stringify({event:"GF_BETA_HEALTH",schema:1,severity:"critical",alerts:["observer_database"]}));}
}};
