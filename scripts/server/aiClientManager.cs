//-----------------------------------------------------------------------------
// Client AI Manager system - coordinates groups of AI units
//-----------------------------------------------------------------------------
// This system should handle messages from units that belong to the client that
// it is assigned to.
$AIClientManager::ThinkTime = 500;
if (!isObject(AIClientManager))
    new ScriptObject(AIClientManager);

function AIClientManager::start(%this, %thinkTime)
{
    MissionCleanup.add(%this);
    %this.thinkTime = (%thinkTime !$= "" ? %thinkTime : $AIClientManager::ThinkTime);

    if (!isObject(%this.messageQue))
        %this.messageQue = new SimSet();
    else
        %this.messageQue.clear();

    if (!isObject(%this.unitList))
        %this.unitList = new SimSet();
    else
        %this.unitList.clear();

    %this.think();

    %this.started = true;
}

/// <summary>
/// This function requests that the AIClientManager spawn a unit and add it to its group.
/// </summary>
/// <param name="name">The desired unit name - this is the SimName of the object and must be unique or "".</param>
/// <param name="spawnLocation">The position or object (spawnpoint, path object) to spawn the unit at.</param>
/// <param name="datablock">The datablock that the unit should use.</param>
/// <param name="priority">The priority of this unit. 0 to 2 from low to high priority.  Defaults to 1.</param>
/// <param name="onPath">If spawnLocation is a path, this should be true to get the unit to spawn on and follow the path.</param>
/// <return>Returns the new unit.</return>
function AIClientManager::addUnit(%this, %name, %spawnLocation, %datablock, %priority, %onPath)
{
	if (%this.client $= "")
	{
		echo(" !!!! AIClientManager is not assigned to a client - cannot add unit");
		return 0;
	}
    %newUnit = AIManager.addUnit(%name, %spawnLocation, %datablock, %priority, %onPath);
    %newUnit.team = (%this.client !$= "" ? %this.client : 0);
    %newUnit.AIClientMan = %this;
    %this.unitList.add(%newUnit);
    return %newUnit;
}

function AIClientManager::removeUnit(%this, %unit)
{
    if (%this.unitList.isMember(%unit))
        %this.unitList.remove(%unit);
    %unit.AIClientMan = "";
    %index = %this.messageQue.getCount() - 1;
    while(%index >= 0)
    {
        %msg = %this.messageQue.getObject(%index);
        %sender = getField(%msg.message, 0);
        if (%sender == %unit)
        {
            %this.messageQue.remove(%msg);
            %msg.delete();
        }
        %index--;
    }
}

/// <summary>
/// This function handles messages from AI Units in the same team
/// </summary>
function AIClientManager::think(%this)
{
    if (%this.client $= "0" || isObject(%this.client))
    {
        %index = %this.messageQue.getCount() - 1;
        while (%index >= 0)
        {
        	// message is no longer valid but somehow did not get cleaned up,
        	// so clean it up.
            %message = %this.messageQue.getObject(%index);
            if (!isObject(%message))
            {
                if (%this.messageQue.isMember(%message))
                {
                    %this.messageQue.remove(%message);
                }
            }
            %this.handleMessage(%message);
            %index--;
        }
    }
    %this.schedule(%this.thinkTime, "think");
}

/// <summary>
/// This method creates a message object and queues it for think() to handle.
/// The message data should be tab-delimited and should contain first the 
/// message originator, then the method to call, and then any parameters for the 
/// desired method.
///
/// AIClientManager.sendMessage(<unit> TAB <handler> TAB <handler parameter1> TAB <etc...>);
/// </summary>
/// <param name="message">The data for the message object.</param>
function AIClientManager::sendMessage(%this, %message)
{
    // We need to create a script object and add it to the message que.
    // In a more interesting implementation there might be a priority system
    // for messages either sorted by multiple queues or by a priority tag on
    // the message objects.
    if (!isObject(%this.messageQue))
        %this.messageQue = new SimSet();
    %msgObj = new ScriptObject();
    // Here we attach the message data to the message object.
    %msgObj.message = %message;
    %this.messageQue.add(%msgObj);
    %this.messageQue.bringToFront(%msgObj);
}

/// <summary>
/// This method parses and handles AI unit messages.
/// </summary>
/// <param name="message">The message object to handle.</param>
function AIClientManager::handleMessage(%this, %message)
{
    // In this case, %message is a script object with a field that carries
    // the data we need to handle the message, so we parse the field for our
    // data.
    %unit = getField(%message.message, 0);
    %unitMessage = getField(%message.message, 1);
    %dataCount = getFieldCount(%message.message);
    if (%dataCount > 1)
    {
        %i = 2;
        while (%i < %dataCount)
        {
            if (%i == 2)
                %data = getField(%message.message, %i);
            else
                %data = %data @ ", " @ getField(%message.message, %i);
            %i++;
        }
    }
    // now that we've parsed this out, check that the message is actually implemented
    // as a handler method on the AIClientManager.
    if (%this.isMethod(%unitMessage))
    {
        // We're good, evaluate the method
        eval("%this."@%unitMessage@"("@%unit@", "@%data@");");
    }
    %this.messageQue.remove(%message);
    %message.delete();
}

/// <summary>
/// This method creates a list of "nearest" allies drawn from our team's unit
/// list.
/// </summary>
/// <param name="unit">The unit that sent the message.</param>
/// <param name="num">The number of allies to gather.</param>
/// <param name="range">The search radius.</param>
function AIClientManager::getNearestAllyList(%this, %unit, %num, %range)
{
    %ally = %this.unitList.getObject(0);
    if (!isObject(%ally) || %ally == 0 || %ally.getState() $= "dead")
    {
        return 0;
    }
    
    %allyList = new SimSet();
    %totalAllies = %this.unitList.getCount();
    %count = %allyList.getCount();
    if (%num > %totalAllies - 1)
        %num = %totalAllies - 1;
    %index = 1;
    %unitPos = %unit.getPosition();
    %dist = %range + 1;
    while (%count < %num)
    {
        %totalAllies = %this.unitList.getCount();
        if (%index >= %totalAllies)
            break;
        if (!isObject(%ally) || %ally == 0 || %ally == %unit || %ally.getState() $= "dead")
        {
            %ally = %this.unitList.getObject(%index);
            %count = %allyList.getCount();
            %index++;
            continue;
        }
        %allyPos = %ally.getPosition();
        %allyDist = VectorDist(%allyPos, %unitPos);
        if (%allyDist > %range)
        {
            %index++;
            continue;
        }
        if (%dist > %tempDist)
        {
            %dist = %tempDist;
            %ally.dist = %dist;
            %allyList.add(%ally);
            %currCount = %allyList.getCount();
            for (%i = 0; %i < %currCount; %i++)
            {
                %obj = %allyList.getObject(%i);
                if (%obj.dist > %dist && %currCount > %num)
                {
                    %allyList.remove(%obj);
                    %currCount--;
                }
            }
        }
        %ally = %this.unitList.getObject(%index);
        %count = %allyList.getCount();
        %index++;
    }
    return %allyList;
}

//-----------------------------------------------------------------------------
// Message handlers
//-----------------------------------------------------------------------------
// Message handlers should be written with the unit as the first non-this parameter
// and other parameters as needed.  See the standard message que handle caller:
//
// eval("%this."@%unitMessage@"("@%unit@", "@%data@");");
//
// So the message is the handler message to call, the unit is the orignator and 
// data is assembled from additional fields passed in the message.
//
// A message is sent by an AI unit from it's datablock think method like so:
//
// %obj.AIClientMan.sendMessage(%obj TAB "underAttack" TAB %damageLvl TAB %obj.damageSourceObj);
//
// %obj is the unit.  It's AIClientManager is assigned to it when it is spawned in
// AIClient::addUnit().  Wherever you send a message from you can use this to do it.
// The message is a tab-delimited string that is expected to be compose as so:
//
// <sending unit> TAB <message name> TAB <tab-delimited handler parameters>
//
// At the moment messages are simply handled in the order received.

/// <summary>
/// A simple message to friendly units that %unit is under attack
/// </summary>
/// <param name="unit">The unit that sent the message.</param>
/// <param name="damage"><unit>'s current damage level.</param>
/// <param name="source">The object that damaged <unit>.</param>
function AIClientManager::underAttack(%this, %unit, %damage, %source)
{
    // %source is most likely a projectile, but whatever it is it should carry a sourceObject
    // field on it that should hold the originating unit (the unit that fired the projectile).
    //echo(" @@@ AI Unit " @ %unit @ " sent message underAttack with data " @ %damage @ ":" @ %source @":"@%source.sourceObject);
    if (%unit.getState() $= "dead")
        return;
    if (%source.sourceObject.team == %unit.team)
    {
        //echo(" @@@ Friendly Fire!");
        return;
    }
    if (isEventPending(%unit.waitForHelp))
        return;

    %unit.waitForHelp = %unit.schedule(2000, think);

    %allyList = %this.getNearestAllyList(%unit, 3, 250);
    if(isObject(%allyList))
    {
        %allyCount = %allyList.getCount();
        if (%allyCount > 0)
        {
            for (%i = 0; %i < %allyCount; %i++)
            {
                %ally = %allyList.getObject(%i);
                if (!isObject(%ally))
                    continue;
                %offsetX = getRandom(-20, 20);
                %offsetY = getRandom(-20, 20);
                %dest = %unit.getPosition();
                %dest.x += %offsetX;
                %dest.y += %offsetY;
                %ally.target = %source.sourceObject;
                %ally.setMoveDestination(%dest);
            }
        }
        %allyList.delete();
    }
}