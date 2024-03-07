import * as fs from "fs"
import * as toml from "toml"
import * as process from "process"
//export const conf;
/* const fs = require('fs');
const process = require('process');
const toml = require('toml'); */
//export {get};

//export function get(){
const tomlFileString = fs.readFileSync(process.argv[2], 'utf-8')
export const conf = toml.parse(tomlFileString)
console.log(conf)
//}
